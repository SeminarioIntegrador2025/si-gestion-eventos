using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.PDFTemplates;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class PagoService : IPagoService
    {
        // --- Campos Privados: Guardan las "herramientas" (dependencias) ---        
        private readonly IGenericRepository<Pago> _pagoRepository;
        private readonly IGenericRepository<ComprobanteExterno> _comprobanteRepository;
        private readonly IMapper _mapper;
        private readonly IValidator<PagoVM> _validator;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEventoService _eventoService;


        public PagoService(
            IGenericRepository<Pago> pagoRepository,
            IGenericRepository<ComprobanteExterno> comprobanteRepository,
            IMapper mapper,
            IValidator<PagoVM> validator,
            IWebHostEnvironment webHostEnvironment,
            IEventoService eventoService)
        {

            _pagoRepository = pagoRepository;
            _comprobanteRepository = comprobanteRepository;
            _mapper = mapper;
            _validator = validator;
            _webHostEnvironment = webHostEnvironment;
            _eventoService = eventoService;
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

                pvm.EventoDescripcion = $"Evento {evento?.Tipo} - {evento?.Inicio:dd/MM/yyyy}"; // '$"{...}"' es interpolación de strings. '?' evita error si evento es null. ':dd/MM/yyyy' formatea la fecha.
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
                if (pago.Evento.Cliente.Tipo == TipoCliente.PersonaJuridica)
                {
                    pagoVM.ClienteNombre = pago.Evento.Cliente.Nombre;
                }

                else
                {
                    pagoVM.ClienteNombre = $"{pago.Evento.Cliente.Nombre} {pago.Evento.Cliente.Apellido}";
                }
            }

            return pagoVM;
        }

        public async Task<List<PagoVM>> GetAllAsync()
        {
            // 1. Obtenemos todos los pagos, incluyendo sus relaciones
            var pagos = await _pagoRepository.FindWithIncludesAsync(
                p => true, // p => true significa "traer todos"
                p => p.ComprobanteExterno,
                p => p.Evento.Cliente
            );

            // 2. Mapeamos a la lista de VMs
            var pagosVM = _mapper.Map<List<PagoVM>>(pagos);

            // 3. (Importante) Llenamos los datos calculados
            foreach (var pvm in pagosVM)
            {
                // Buscamos la entidad original para acceder a las relaciones cargadas
                var pagoEntity = pagos.FirstOrDefault(p => p.PagoId == pvm.PagoId);

                if (pagoEntity?.Evento != null)
                {
                    pvm.EventoDescripcion = $"Evento {pagoEntity.Evento.Tipo} - {pagoEntity.Evento.Inicio:dd/MM/yyyy}";

                    if (pagoEntity.Evento.Cliente != null)
                    {
                        pvm.ClienteNombre = $"{pagoEntity.Evento.Cliente.Nombre} {pagoEntity.Evento.Cliente.Apellido}";
                    }
                    else
                    {
                        pvm.ClienteNombre = "Cliente no especificado";
                    }
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
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<PagoVM>.FailureResult(errors);
            }

            var pagoEntity = _mapper.Map<Pago>(pagoVM);
            pagoEntity.ComprobanteExterno = null;

            ComprobanteExterno? comprobanteEntity = null;

            if (pagoVM.ArchivoComprobante != null && pagoVM.ArchivoComprobante.Length > 0)
            {
                try
                {
                    comprobanteEntity = new ComprobanteExterno
                    {
                        PagoId = 0,
                        Pago = pagoEntity,
                        NombreArchivo = pagoVM.ArchivoComprobante.FileName,
                        RutaArchivo = string.Empty,
                        FechaComprobante = DateTime.Now,
                        TipoArchivo = pagoVM.TipoArchivoComprobante ?? TipoArchivo.PDF,
                    };

                    comprobanteEntity.RutaArchivo = await GuardarArchivoComprobanteAsync(pagoVM.ArchivoComprobante, pagoVM.EventoId, 0);
                    pagoEntity.ComprobanteExterno = comprobanteEntity;
                }
                catch (IOException ex)
                {
                    return ServiceResult<PagoVM>.FailureResult($"Error al guardar el archivo: {ex.Message}");
                }
                catch (Exception ex)
                {
                    return ServiceResult<PagoVM>.FailureResult("Ocurrió un error inesperado al procesar el archivo del comprobante.");
                }
            }

            try
            {
                await _pagoRepository.AddAsync(pagoEntity);
                await _pagoRepository.SaveChangesAsync();

                var evento = await _eventoService.GetByIdAsync(pagoVM.EventoId);

                if (evento != null)
                {
                    // Calcular el costo total del evento
                    float costoTotal = (float)(evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0));

                    // Calcular el total pagado (incluyendo el pago que acabamos de registrar)
                    float totalPagado = (float)evento.TotalPagado + pagoEntity.Monto;

                    // Actualizar el estado según el saldo
                    if (totalPagado >= costoTotal && evento.Estado == EventoEstado.PendienteAdeudado)
                    {
                        // Crear un EventoVM para actualizar (necesario para usar el servicio)
                        var eventoParaActualizar = await _eventoService.GetByIdAsync(evento.EventoId);

                        if (eventoParaActualizar != null)
                        {
                            eventoParaActualizar.Estado = EventoEstado.PendientePagado;
                            await _eventoService.UpdateAsync(eventoParaActualizar);
                        }
                    }
                }
                var pagoGuardadoVM = await GetByIdAsync(pagoEntity.PagoId);
                return ServiceResult<PagoVM>.SuccessResult(pagoGuardadoVM!, "Pago registrado exitosamente.");
            }
            catch (DbUpdateException ex)
            {
                return ServiceResult<PagoVM>.FailureResult("Error al guardar en la base de datos. Verifique las relaciones o datos.");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(comprobanteEntity?.RutaArchivo))
                {
                    BorrarArchivoComprobante(comprobanteEntity.RutaArchivo);
                }
                return ServiceResult<PagoVM>.FailureResult("Ocurrió un error inesperado al guardar el pago.");
            }
        }
        //Helpers

        private async Task<string> GuardarArchivoComprobanteAsync(IFormFile archivo, int eventoId, int pagoId_AunNoGenerado) // Renombrado para claridad
        {
            // Validación básica
            if (archivo == null || archivo.Length == 0)
            {
                throw new ArgumentException("Archivo inválido o vacío."); // Lanza un error si el archivo es incorrecto
            }

            // --- Construcción de la Ruta ---
            // 1. Obtiene la ruta de la carpeta 'wwwroot' (donde van los archivos públicos web)
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            // 2. Define la carpeta base DENTRO de wwwroot
            string carpetaBase = Path.Combine(wwwRootPath, "uploads", "comprobantes");
            // 3. Crea una subcarpeta específica para este evento (si no existe)
            string carpetaEvento = Path.Combine(carpetaBase, eventoId.ToString());
            Directory.CreateDirectory(carpetaEvento); // No hace nada si ya existe

            // --- Generación del Nombre Único ---         
            string extension = Path.GetExtension(archivo.FileName);
            // 5. Crea GUID
            string nombreUnico = $"{Guid.NewGuid()}{extension}";
            // 6. Combina la ruta de la carpeta del evento con el nombre único para obtener la ruta completa donde se guardará.
            string rutaCompleta = Path.Combine(carpetaEvento, nombreUnico);

            // --- Guardado del Archivo ---
            // 7. Abre un flujo de archivo (FileStream) en la ruta completa, en modo Creación (sobreescribe si existe).
            //    'using' asegura que el stream se cierre correctamente aunque haya errores.
            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                // 8. Copia el contenido del archivo subido (archivo.CopyToAsync) al flujo del archivo en el servidor.
                //    'await' espera a que la copia termine.
                await archivo.CopyToAsync(stream);
            }

            // --- Devolución de la Ruta Relativa ---
            // 9. Construye la ruta RELATIVA (la que se guarda en la BD).
            //    Empieza con '/' para indicar que es relativa a la raíz del sitio web.
            string rutaRelativa = $"/uploads/comprobantes/{eventoId}/{nombreUnico}";

            // 10. Devuelve la ruta relativa.
            return rutaRelativa;
        }

        private void BorrarArchivoComprobante(string? rutaRelativa)
        {
            if (string.IsNullOrEmpty(rutaRelativa)) return;

            try
            {
                string rutaAbsoluta = Path.Combine(_webHostEnvironment.WebRootPath, rutaRelativa.TrimStart('/'));

                // Comprueba si el archivo existe en esa ruta absoluta.
                if (File.Exists(rutaAbsoluta))
                {
                    // Si existe, lo borra.
                    File.Delete(rutaAbsoluta);
                }
            }
            catch (Exception ex)
            {
                // Si ocurre un error al borrar, lo ideal es loguearlo.
                // Usamos Console.WriteLine como ejemplo simple.
                // NO lanzamos el error de nuevo ('throw') para no interrumpir
                // la operación principal (ej: el borrado del pago en la BD).
                Console.WriteLine($"Error al borrar archivo {rutaRelativa}: {ex.Message}");
            }
        }

        public async Task<byte[]?> GenerarReciboPdfAsync(int pagoId)
        {
            var pagoVM = await GetByIdAsync(pagoId);

            if (pagoVM == null)
            {
                return null;
            }

            var documento = new ReciboPagoDocument(pagoVM);
            return documento.GeneratePdf();
        }
    }
}

