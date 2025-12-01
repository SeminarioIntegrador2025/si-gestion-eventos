using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class EventoService : IEventoService
    {
        private readonly IGenericRepository<Evento> _eventoRepository;
        private readonly IValidator<EventoVM> _validator;
        private readonly IEventoBusinessRules _businessRules;
        private readonly IMapper _mapper;
        private readonly IGenericRepository<Pago> _pagoRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IGenericRepository<ComprobanteExterno> _comprobanteRepository;

        public EventoService(
            IGenericRepository<Evento> eventoRepository,
            IGenericRepository<Pago> pagoRepository,
            IValidator<EventoVM> validator,
            IEventoBusinessRules businessRules,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IGenericRepository<ComprobanteExterno> comprobanteRepository)
        {
            _pagoRepository = pagoRepository;
            _eventoRepository = eventoRepository;
            _validator = validator;
            _businessRules = businessRules;
            _mapper = mapper;
            _fileStorageService = fileStorageService;
            _comprobanteRepository = comprobanteRepository;
        }
        public async Task<PaginatedList<EventoVM>> GetAllPaginatedAsync(
            string? searchQuery,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EventoEstado? estado,
            string ordenarPor,
            int page,
            int pageSize)
        {
            try
            {
                Expression<Func<Evento, bool>> textPredicate = e =>
                    string.IsNullOrEmpty(searchQuery) ||
                    (e.Cliente.Nombre + " " + e.Cliente.Apellido).Contains(searchQuery) ||
                    e.Cliente.CedulaIdentidad.Contains(searchQuery) ||
                    e.Tipo.ToString().Contains(searchQuery);

                var eventosQuery = (await _eventoRepository.FindWithIncludesAsync(
                                    textPredicate,
                                    q => q.Cliente
                                )).AsQueryable();

                if (fechaDesde.HasValue)
                {
                    eventosQuery = eventosQuery.Where(e => e.Inicio.Date >= fechaDesde.Value.Date);
                }
                if (fechaHasta.HasValue)
                {
                    eventosQuery = eventosQuery.Where(e => e.Inicio.Date <= fechaHasta.Value.Date);
                }

                if (estado.HasValue)
                {
                    eventosQuery = eventosQuery.Where(e => e.Estado == estado.Value);
                }

                IOrderedQueryable<Evento> eventosOrdenados = ordenarPor switch
                {
                    "fecha_inicio_desc" => eventosQuery.OrderByDescending(e => e.Inicio),
                    "fecha_contrato_asc" => eventosQuery.OrderBy(e => e.FechaContrato),
                    "fecha_contrato_desc" => eventosQuery.OrderByDescending(e => e.FechaContrato),
                    "cantidad_personas_desc" => eventosQuery.OrderByDescending(e => e.CantidadPersonas),
                    "cantidad_personas_asc" => eventosQuery.OrderBy(e => e.CantidadPersonas),
                    "tipo_evento" => eventosQuery.OrderBy(e => e.Tipo),
                    _ => eventosQuery.OrderBy(e => e.Inicio)
                };

                var totalCount = eventosOrdenados.Count();
                var eventosPaginados = eventosOrdenados.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                var items = _mapper.Map<List<EventoVM>>(eventosPaginados);

                return new PaginatedList<EventoVM>(items, totalCount, page, pageSize);
            }
            catch (Exception ex)
            {
                // Loggear el error
                return new PaginatedList<EventoVM>(new List<EventoVM>(), 0, page, pageSize);
            }
        }

        // --- GetByIdAsync (Corregido para CostoTotal) ---
        public async Task<EventoVM?> GetByIdAsync(int id)
        {
            var evento = await _eventoRepository.GetByIdWithIncludesAsync(id, e => e.Cliente, e => e.Pagos, e => e.Fianza);

            if (evento == null)
                return null;

            var eventoVM = _mapper.Map<EventoVM>(evento);

            // Calcula el total pagado
            eventoVM.TotalPagado = (decimal)(evento.Pagos?.Sum(p => p.Monto) ?? 0);

            // Calcula el costo total real
            float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));

            // Calcula el saldo restante
            eventoVM.SaldoRestante = (decimal)(costoTotal) - (eventoVM.TotalPagado);

            return eventoVM;
        }

        // --- CreateAsync ---
        public async Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM)
        {
            // Validar usando el RuleSet "Create" + reglas comunes
            var validationResult = await _validator.ValidateAsync(eventoVM, options =>
                options.IncludeRuleSets("Create"));

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<EventoVM>.FailureResult(errors);
            }

                bool isAvailable = await _businessRules.IsDateRangeAvailableAsync(
                    eventoVM.Inicio, eventoVM.Fin, eventoVM.HoraInicio, eventoVM.HoraFin, null);


            if (!isAvailable)
            {
                return ServiceResult<EventoVM>.FailureResult("El horario seleccionado ya no está disponible...");
            }

            try
            {
                var evento = _mapper.Map<Evento>(eventoVM);
                evento.Estado = EventoEstado.PendienteAdeudado;

                await _eventoRepository.AddAsync(evento);
                await _eventoRepository.SaveChangesAsync();
            

                var eventoCreado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                var eventoVM_Creado = _mapper.Map<EventoVM>(eventoCreado);

                if (eventoCreado == null)
                {
                    return ServiceResult<EventoVM>.FailureResult("Error al recuperar el evento recién creado.");
                }
                return ServiceResult<EventoVM>.SuccessResult(eventoVM_Creado, "Evento creado exitosamente.");
            }
            catch (Exception ex)
            {
                // Loggear error
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al crear el evento.");
            }
        }

        // --- CreateEventWithPaymentAsync ---
        public async Task<ServiceResult<EventoVM>> CreateEventWithPaymentAsync(EventoVM eventoVM, PagoReservaVM pagoVM)
        {
            var validationResult = await _validator.ValidateAsync(eventoVM, options =>
                options.IncludeRuleSets("Create"));

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<EventoVM>.FailureResult(errors);
            }

            bool isAvailable = await _businessRules.IsDateRangeAvailableAsync(
                eventoVM.Inicio, eventoVM.Fin, eventoVM.HoraInicio, eventoVM.HoraFin, null);

            if (!isAvailable)
            {
                return ServiceResult<EventoVM>.FailureResult("El horario seleccionado ya no está disponible.");
            }

            string urlComprobante = string.Empty;

          
            try
            {
                var evento = _mapper.Map<Evento>(eventoVM);
                float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));
                float montoPagado = pagoVM.Monto;

                if (montoPagado >= costoTotal)
                {
                    evento.Estado = EventoEstado.PendientePagado;
                }
                else
                {
                    evento.Estado = EventoEstado.PendienteAdeudado;
                }

                await _eventoRepository.AddAsync(evento);
                await _eventoRepository.SaveChangesAsync();

                var pago = new Pago
                {
                    EventoId = evento.EventoId,
                    Monto = pagoVM.Monto,
                    Fecha = pagoVM.Fecha,
                    Observaciones = pagoVM.Observaciones,
                    Metodo = pagoVM.Metodo
                };

                await _pagoRepository.AddAsync(pago);
                await _pagoRepository.SaveChangesAsync();

                if (pagoVM.ArchivoComprobante != null && pagoVM.ArchivoComprobante.Length > 0)
                {
                    urlComprobante = await _fileStorageService.GuardarArchivoAsync(
                        pagoVM.ArchivoComprobante,
                        "uploads/comprobantes"
                    );

                    if (string.IsNullOrEmpty(urlComprobante))
                    {
                        throw new InvalidOperationException("Se adjuntó un archivo, pero ocurrió un error al guardarlo.");
                    }

                    var comprobante = new ComprobanteExterno
                    {
                        NombreArchivo = pagoVM.ArchivoComprobante.FileName,
                        RutaArchivo = urlComprobante,
                        FechaComprobante = DateTime.UtcNow,
                        TipoArchivo = ConvertExtensionToTipoArchivo(pagoVM.ArchivoComprobante.FileName),
                        Referencia = null,
                        PagoId = pago.PagoId,
                        Pago = pago
                    };

                    await _comprobanteRepository.AddAsync(comprobante);
                    await _comprobanteRepository.SaveChangesAsync();

                    pago.ComprobanteExternoId = comprobante.ComprobanteExternoId;
                    _pagoRepository.Update(pago);
                    await _pagoRepository.SaveChangesAsync();
                }

                var eventoCreado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                var eventoVM_Creado = _mapper.Map<EventoVM>(eventoCreado);

                return ServiceResult<EventoVM>.SuccessResult(eventoVM_Creado, "Evento y pago de reserva creados exitosamente.");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(urlComprobante))
                {
                    await _fileStorageService.BorrarArchivoAsync(urlComprobante);
                }
                return ServiceResult<EventoVM>.FailureResult($"Ocurrió un error: {ex.Message}");
            }
        }

        public async Task<IEnumerable<SelectListItem>> GetEventosSinFianzaParaDropdownAsync()
        {

            var eventosDisponibles = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado != EventoEstado.Cancelado &&
                     e.FianzaId == null, 
                e => e.Cliente 
            );

            // Si no hay eventos, devolvemos lista vacía
            if (eventosDisponibles == null || !eventosDisponibles.Any())
            {
                return new List<SelectListItem>();
            }

            // Mapeamos a SelectListItem para el dropdown
            return eventosDisponibles
                .OrderBy(e => e.Inicio) // Ordenados por fecha
                .Select(e => new SelectListItem
                {
                    Value = e.EventoId.ToString(),
                    // Texto: "Juan Perez - Cumpleaños (15/11/2025)"
                    Text = $"{e.Cliente.Nombre} {e.Cliente.Apellido} - {e.Tipo} ({e.Inicio:dd/MM/yyyy})"
                });
        }

        // --- SERVICIOS DE FONDO ---

        public async Task<List<EventoVM>> GetAlertasServiciosAsync()
        {
            var deadline = DateTime.Now.AddHours(48);
            var now = DateTime.Now;

            var eventosEnPeligro = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado != EventoEstado.Cancelado &&
                     e.Estado != EventoEstado.Realizado &&
                     e.Inicio > now &&
                     e.Inicio < deadline &&
                    !e.ServiciosEsenciales.OfType<CertificadoAGADU>().Any(c => c.Verificado),
                e => e.Cliente,
                e => e.ServiciosEsenciales
            );

            return _mapper.Map<List<EventoVM>>(eventosEnPeligro);
        }
        public async Task<ServiceResult<int>> MarkCompletedEventsAsync()
        {    
            int completedCount = 0;
            var statesToComplete = new[] {
                EventoEstado.PendienteAdeudado,
                EventoEstado.PendientePagado,
                EventoEstado.Reprogramado
            };
            try
            {
                var eventsToMark = await _eventoRepository.FindAsync(
                    e => e.Fin.Date < DateTime.Today &&
                         statesToComplete.Contains(e.Estado)
                );
                if (!eventsToMark.Any())
                {
                    return ServiceResult<int>.SuccessResult(0, "No hay eventos para marcar como realizados.");
                }
                foreach (var evento in eventsToMark)
                {
                    evento.Estado = EventoEstado.Realizado;
                    _eventoRepository.Update(evento);
                    completedCount++;
                }
                if (completedCount > 0)
                {
                    await _eventoRepository.SaveChangesAsync();
                }
                return ServiceResult<int>.SuccessResult(completedCount, $"Se marcaron {completedCount} eventos como Realizados.");
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.FailureResult($"Error al marcar eventos como realizados: {ex.Message}");
            }
        }

        public async Task<ServiceResult<int>> CheckAndCancelUnpaidEventsAsync()
        {
            var deadline = DateTime.Now.AddHours(48);
            var now = DateTime.Now;
            int cancelledCount = 0;
            try
            {
                var eventsToCancelQuery = await _eventoRepository.FindWithIncludesAsync(
                    e => e.Estado != EventoEstado.Cancelado &&
                         e.Inicio < deadline &&
                         e.Inicio > now, 
                    e => e.Pagos
                );

                var eventsToCancel = eventsToCancelQuery.ToList();
                if (!eventsToCancel.Any())
                {
                    return ServiceResult<int>.SuccessResult(0, "No hay eventos por vencer.");
                }
                foreach (var evento in eventsToCancel)
                {
                    float costoTotal = evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0);
                    float totalPagado = evento.Pagos?.Sum(p => p.Monto) ?? 0;

                    // Solo cancelamos si no pagaron NADA.
                    if (totalPagado == 0)
                    {
                        evento.Estado = EventoEstado.Cancelado;
                        evento.Observaciones = (evento.Observaciones ?? "") +
                            $" [Cancelado automáticamente por falta de pago 48hs antes.]";
                        _eventoRepository.Update(evento);
                        cancelledCount++;
                    }
                }
                if (cancelledCount > 0)
                {
                    await _eventoRepository.SaveChangesAsync();
                    return ServiceResult<int>.SuccessResult(cancelledCount, $"Se cancelaron {cancelledCount} eventos.");
                }

                return ServiceResult<int>.SuccessResult(0, "Eventos por vencer están todos pagos (o con reserva).");
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.FailureResult($"Error en el servicio de cancelación: {ex.Message}");
            }
        }

        // --- MÉTODOS RESTANTES  ---

        public async Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM)
        {

            var validationResult = await _validator.ValidateAsync(eventoVM, options => options.IncludeRuleSets("default"));
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<EventoVM>.FailureResult(errors);
            }
            try
            {
                var evento = await _eventoRepository.GetByIdWithIncludesAsync(eventoVM.EventoId, e => e.Cliente);
                if (evento == null) { return ServiceResult<EventoVM>.FailureResult("Evento no encontrado."); }
                if (!await _businessRules.IsClienteActiveAsync(evento.ClienteId)) { return ServiceResult<EventoVM>.FailureResult("...cliente está inactivo."); }
            

                var fechaContratoOriginal = evento.FechaContrato;
                var clienteIdOriginal = evento.ClienteId;
                var costoAlquilerOriginal = evento.CostoAlquiler;
                var montoReservaOriginal = evento.MontoReserva;

                _mapper.Map(eventoVM, evento);

                evento.FechaContrato = fechaContratoOriginal;
                evento.ClienteId = clienteIdOriginal;
                evento.CostoAlquiler = costoAlquilerOriginal;
                evento.MontoReserva = montoReservaOriginal;

                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();
                var eventoActualizado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                var eventoVM_Actualizado = _mapper.Map<EventoVM>(eventoActualizado);
                return ServiceResult<EventoVM>.SuccessResult(eventoVM_Actualizado, "Evento actualizado correctamente.");
            }
            catch (Exception ex) { return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al actualizar."); }
        }

        public async Task<ServiceResult<bool>> CancelAsync(int id)
        {
            try
            {
                if (!await _businessRules.CanCancelEventoAsync(id)) { return ServiceResult<bool>.FailureResult("No se puede cancelar..."); }
                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null) { return ServiceResult<bool>.FailureResult("Evento no encontrado."); }
                evento.Estado = EventoEstado.Cancelado;
                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido cancelado correctamente.");
            }
            catch (Exception ex) { return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al cancelar."); }
        }

        public async Task<ServiceResult<bool>> RescheduleAsync(int id, DateTime nuevaFechaInicio, DateTime nuevaFechaFin, TimeSpan nuevaHoraInicio, TimeSpan nuevaHoraFin)
        {
            try
            {
                if (!await _businessRules.CanRescheduleEventoAsync(id)) { return ServiceResult<bool>.FailureResult("No se puede reprogramar..."); }
                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null) { return ServiceResult<bool>.FailureResult("Evento no encontrado."); }
                if (!await _businessRules.IsValidDateRangeAsync(nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin)) { return ServiceResult<bool>.FailureResult("El nuevo rango no es válido."); }
                if (!await _businessRules.IsDateRangeAvailableAsync(nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin, id)) { return ServiceResult<bool>.FailureResult("Ya existe otro evento..."); }

                evento.Inicio = nuevaFechaInicio;
                evento.Fin = nuevaFechaFin;
                evento.HoraInicio = nuevaHoraInicio;
                evento.HoraFin = nuevaHoraFin;
                evento.Estado = EventoEstado.Reprogramado;
                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido reprogramado correctamente.");
            }
            catch (Exception ex) { return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al reprogramar."); }
        }

        public async Task<ServiceResult<bool>> ConfirmAsync(int id)
        {
            try
            {
                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null) { return ServiceResult<bool>.FailureResult("Evento no encontrado."); }
                if (evento.Estado == EventoEstado.Cancelado) { return ServiceResult<bool>.FailureResult("No se puede confirmar un evento cancelado."); }
                if (evento.Estado == EventoEstado.PendienteAdeudado) { return ServiceResult<bool>.FailureResult("El evento ya se encuentra pendiente y adeudado."); }
                if (evento.Estado == EventoEstado.PendientePagado) { return ServiceResult<bool>.FailureResult("El evento ya se encuentra pendiente y pagado."); }
                if (!await _businessRules.IsEventoInFutureAsync(evento.Inicio)) { return ServiceResult<bool>.FailureResult("No se puede confirmar un evento que ya pasó."); }

                evento.Estado = EventoEstado.PendienteAdeudado;
                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido confirmado correctamente.");
            }
            catch (Exception ex) { return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al confirmar."); }
        }

        public async Task<List<EventoVM>> GetLatestAsync(int count)
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(null, e => e.Cliente, e => e.Cliente);
            var eventosOrdenados = eventos.OrderByDescending(e => e.EventoId).Take(count);
            return _mapper.Map<List<EventoVM>>(eventosOrdenados);
        }

        public async Task<IEnumerable<SelectListItem>> GetTiposEventoParaDropdownAsync()
        {
            var tiposEvento = Enum.GetValues<TipoEvento>().Select(tipo => new SelectListItem { Value = tipo.ToString(), Text = tipo.ToString() });
            return await Task.FromResult(tiposEvento);
        }

        public async Task<bool> CanModifyEventoAsync(int eventoId)
        {
            return await _businessRules.CanModifyEventoAsync(eventoId);
        }

        public async Task<bool> HasConflictingEventsAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin, int? excludeEventoId = null)
        {
            return !await _businessRules.IsDateRangeAvailableAsync(inicio, fin, horaInicio, horaFin, excludeEventoId);
        }

        public async Task<IEnumerable<SelectListItem>> GetEventosAdeudadosParaDropdownAsync()
        {
            var eventosAdeudado = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado == EventoEstado.PendienteAdeudado,
                e => e.Cliente
            );
            if (eventosAdeudado == null || !eventosAdeudado.Any())
            {
                return new List<SelectListItem>();
            }
            return eventosAdeudado.OrderBy(e => e.Inicio).Select(e => new SelectListItem
            {
                Value = e.EventoId.ToString(),
                Text = $"{e.Cliente.Nombre} {e.Cliente?.Apellido} - {e.Tipo} - {e.Inicio:dd/MM/yyyy}"
            });
        }

        // --- Helper de Conversión de Tipo de Archivo  ---
        private TipoArchivo ConvertExtensionToTipoArchivo(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new InvalidOperationException("El archivo no tiene nombre o extensión.");

            string extension = System.IO.Path.GetExtension(fileName).ToLower();

            switch (extension)
            {
                case ".pdf":
                    return TipoArchivo.PDF;
                case ".png":
                    return TipoArchivo.PNG;
                case ".jpeg":
                    return TipoArchivo.JPEG;
                case ".jpg":
                    return TipoArchivo.JPG;
                default:
                    throw new InvalidOperationException($"Tipo de archivo no permitido: {extension}. Solo se aceptan PDF, PNG, JPEG o JPG.");
            }
        }

      
    }
}