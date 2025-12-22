using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.PDFTemplates; // Asegúrate de que esta ruta sea correcta para tu Recibo
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class PagoService : IPagoService
    {
        private readonly IGenericRepository<Pago> _pagoRepository;
        private readonly IGenericRepository<ComprobanteExterno> _comprobanteRepository;
        private readonly IMapper _mapper;
        private readonly IValidator<PagoVM> _validator;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEventoService _eventoService;
        // FIX CS0103: Agregamos la herramienta de almacenamiento de archivos que faltaba
        private readonly IFileStorageService _fileStorageService;

        public PagoService(
            IGenericRepository<Pago> pagoRepository,
            IGenericRepository<ComprobanteExterno> comprobanteRepository,
            IMapper mapper,
            IValidator<PagoVM> validator,
            IWebHostEnvironment webHostEnvironment,
            IEventoService eventoService,
            IFileStorageService fileStorageService) // Inyectamos el servicio aquí
        {
            _pagoRepository = pagoRepository;
            _comprobanteRepository = comprobanteRepository;
            _mapper = mapper;
            _validator = validator;
            _webHostEnvironment = webHostEnvironment;
            _eventoService = eventoService;
            _fileStorageService = fileStorageService; // Asignamos la inyección
        }

        public async Task<List<PagoVM>> GetPagosByEventoIdAsync(int eventoId)
        {
            var pagos = await _pagoRepository.FindWithIncludesAsync(
                p => p.EventoId == eventoId,
                p => p.ComprobanteExterno);

            var pagosVM = _mapper.Map<List<PagoVM>>(pagos);
            var evento = await _eventoService.GetByIdAsync(eventoId);

            foreach (var pvm in pagosVM)
            {
                pvm.EventoDescripcion = $"Evento {evento?.Tipo} - {evento?.Inicio:dd/MM/yyyy}";
                pvm.ClienteNombre = evento?.ClienteNombreCompleto;
                pvm.RutaArchivoExistente = pagos.FirstOrDefault(p => p.PagoId == pvm.PagoId)?.ComprobanteExterno?.RutaArchivo;
            }
            return pagosVM;
        }

        public async Task<PagoVM?> GetByIdAsync(int id)
        {
            var pago = await _pagoRepository.GetByIdWithIncludesAsync(id, p => p.ComprobanteExterno, p => p.Evento.Cliente);
            if (pago == null) return null;

            var pagoVM = _mapper.Map<PagoVM>(pago);
            pagoVM.EventoDescripcion = $"Evento {pago.Evento?.Tipo} - {pago.Evento?.Inicio:dd/MM/yyyy}";
            pagoVM.RutaArchivoExistente = pago.ComprobanteExterno?.RutaArchivo;

            if (pago.Evento?.Cliente != null)
            {
                pagoVM.ClienteNombre = pago.Evento.Cliente.Tipo == TipoCliente.PersonaJuridica
                    ? pago.Evento.Cliente.Nombre
                    : $"{pago.Evento.Cliente.Nombre} {pago.Evento.Cliente.Apellido}";
            }

            return pagoVM;
        }

        public async Task<List<PagoVM>> GetAllAsync()
        {
            var pagos = await _pagoRepository.FindWithIncludesAsync(
                p => true,
                p => p.ComprobanteExterno,
                p => p.Evento.Cliente
            );

            var pagosVM = _mapper.Map<List<PagoVM>>(pagos);

            foreach (var pvm in pagosVM)
            {
                var pagoEntity = pagos.FirstOrDefault(p => p.PagoId == pvm.PagoId);
                if (pagoEntity?.Evento != null)
                {
                    pvm.EventoDescripcion = $"Evento {pagoEntity.Evento.Tipo} - {pagoEntity.Evento.Inicio:dd/MM/yyyy}";
                    pvm.ClienteNombre = pagoEntity.Evento.Cliente != null
                        ? $"{pagoEntity.Evento.Cliente.Nombre} {pagoEntity.Evento.Cliente.Apellido}"
                        : "Cliente no especificado";
                }
                pvm.RutaArchivoExistente = pagoEntity?.ComprobanteExterno?.RutaArchivo;
            }
            return pagosVM;
        }

        public async Task<ServiceResult<PagoVM>> CreateAsync(PagoVM pagoVM)
        {
            var validationResult = await _validator.ValidateAsync(pagoVM);
            if (!validationResult.IsValid)
            {
                return ServiceResult<PagoVM>.FailureResult(validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var pagoEntity = _mapper.Map<Pago>(pagoVM);
            pagoEntity.ComprobanteExterno = null;

            if (pagoVM.ArchivoComprobante != null && pagoVM.ArchivoComprobante.Length > 0)
            {
                try
                {
                    var comprobanteEntity = new ComprobanteExterno
                    {
                        PagoId = 0,
                        Pago = pagoEntity,
                        NombreArchivo = pagoVM.ArchivoComprobante.FileName,
                        RutaArchivo = string.Empty,
                        FechaComprobante = DateTime.Now,
                        TipoArchivo = ConvertExtensionToTipoArchivo(pagoVM.ArchivoComprobante.FileName)
                    };

                    comprobanteEntity.RutaArchivo = await GuardarArchivoComprobanteAsync(pagoVM.ArchivoComprobante, pagoVM.EventoId, 0);
                    pagoEntity.ComprobanteExterno = comprobanteEntity;
                }
                catch (Exception ex)
                {
                    return ServiceResult<PagoVM>.FailureResult($"Error al procesar el archivo: {ex.Message}");
                }
            }

            try
            {
                await _pagoRepository.AddAsync(pagoEntity);
                await _pagoRepository.SaveChangesAsync();

                // Recalcular estado del evento
                var evento = await _eventoService.GetByIdAsync(pagoVM.EventoId);

                if (evento != null)
                {
                    // LÓGICA INFALIBLE:
                    // Si el saldo es positivo (mayor a 0), SIEMPRE es Adeudado.
                    // Si el saldo es 0 o negativo (por centavos), es Pagado.
                    // Usamos decimal para evitar errores de precisión de float.

                    if (evento.SaldoRestante > 0)
                    {
                        evento.Estado = EventoEstado.PendienteAdeudado;
                    }
                    else
                    {
                        evento.Estado = EventoEstado.PendientePagado;
                    }

                    await _eventoService.UpdateAsync(evento);
                }

                var pagoGuardadoVM = await GetByIdAsync(pagoEntity.PagoId);
                return ServiceResult<PagoVM>.SuccessResult(pagoGuardadoVM!, "Pago registrado exitosamente.");
            }
            catch (Exception)
            {
                return ServiceResult<PagoVM>.FailureResult("Ocurrió un error al guardar el pago.");
            }
        }

        public async Task<ServiceResult<bool>> UpdateAsync(PagoVM model)
        {
            try
            {
                // 1. Cargamos el pago incluyendo el comprobante actual
                var pago = await _pagoRepository.GetByIdWithIncludesAsync(model.PagoId, p => p.ComprobanteExterno);
                if (pago == null) return ServiceResult<bool>.FailureResult("El pago no existe.");

                // 2. Actualizamos datos básicos
                pago.Metodo = model.Metodo;
                pago.Observaciones = model.Observaciones;

                // 3. Gestión de Archivos: ¿El usuario subió un archivo nuevo?
                if (model.ArchivoComprobante != null && model.ArchivoComprobante.Length > 0)
                {
                    // A. Si YA EXISTÍA un comprobante, lo eliminamos (Limpieza)
                    if (pago.ComprobanteExterno != null)
                    {
                        // Borramos el archivo físico usando tu Helper
                        BorrarArchivoComprobante(pago.ComprobanteExterno.RutaArchivo);

                        // Borramos el registro de la base de datos
                        _comprobanteRepository.Remove(pago.ComprobanteExterno);
                    }

                    // B. Guardamos el NUEVO archivo físico
                    string url = await GuardarArchivoComprobanteAsync(model.ArchivoComprobante, model.EventoId, pago.PagoId);

                    // C. Creamos el NUEVO registro en la BD
                    var nuevoComprobante = new ComprobanteExterno
                    {
                        PagoId = pago.PagoId,
                        Pago = pago,
                        NombreArchivo = model.ArchivoComprobante.FileName,
                        RutaArchivo = url,
                        FechaComprobante = DateTime.Now,
                        TipoArchivo = ConvertExtensionToTipoArchivo(model.ArchivoComprobante.FileName)
                    };

                    await _comprobanteRepository.AddAsync(nuevoComprobante);
                }

                // 4. Guardamos todos los cambios (Update de pago y cambios en comprobante)
                _pagoRepository.Update(pago);
                await _pagoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "Pago y comprobante actualizados correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Error técnico: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> AnularPagoAsync(int pagoId)
        {
            var pago = await _pagoRepository.GetByIdAsync(pagoId);
            if (pago == null || !pago.Valido) return ServiceResult<bool>.FailureResult("Pago no encontrado o ya anulado.");

            try
            {
                pago.Valido = false;
                pago.Observaciones = $"{pago.Observaciones} [Anulado: {DateTime.Now:dd/MM/yyyy HH:mm}]".Trim();

                var evento = await _eventoService.GetByIdAsync(pago.EventoId);
                if (evento != null)
                {
                    evento.TotalPagado -= (decimal)pago.Monto;
                    decimal costoTotal = (decimal)evento.CostoAlquiler + (decimal)(evento.MontoAireAcondicionado ?? 0);

                    if (evento.TotalPagado < costoTotal && evento.Estado == EventoEstado.PendientePagado)
                    {
                        evento.Estado = EventoEstado.PendienteAdeudado;
                        await _eventoService.UpdateAsync(evento);
                    }
                }

                _pagoRepository.Update(pago);
                await _pagoRepository.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true, "Pago anulado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Error: {ex.Message}");
            }
        }

        // --- HELPERS ---

        // FIX CS0103: Agregado el método que faltaba para convertir extensiones
        private TipoArchivo ConvertExtensionToTipoArchivo(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".pdf" => TipoArchivo.PDF,
                ".png" => TipoArchivo.PNG,
                ".jpeg" => TipoArchivo.JPEG,
                ".jpg" => TipoArchivo.JPG,
                _ => TipoArchivo.PDF // Por defecto
            };
        }

        private async Task<string> GuardarArchivoComprobanteAsync(IFormFile archivo, int eventoId, int pagoId)
        {
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string carpetaEvento = Path.Combine(wwwRootPath, "uploads", "comprobantes", eventoId.ToString());
            Directory.CreateDirectory(carpetaEvento);

            string nombreUnico = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";
            string rutaCompleta = Path.Combine(carpetaEvento, nombreUnico);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return $"/uploads/comprobantes/{eventoId}/{nombreUnico}";
        }

        private void BorrarArchivoComprobante(string? rutaRelativa)
        {
            if (string.IsNullOrEmpty(rutaRelativa)) return;
            try
            {
                string rutaAbsoluta = Path.Combine(_webHostEnvironment.WebRootPath, rutaRelativa.TrimStart('/'));
                if (File.Exists(rutaAbsoluta)) File.Delete(rutaAbsoluta);
            }
            catch (Exception ex) { Console.WriteLine($"Error al borrar: {ex.Message}"); }
        }

        public async Task<byte[]?> GenerarReciboPdfAsync(int pagoId)
        {
            var pagoVM = await GetByIdAsync(pagoId);
            if (pagoVM == null) return null;


            var documento = new ReciboPagoDocument(pagoVM);
            return documento.GeneratePdf();
        }
    }
}