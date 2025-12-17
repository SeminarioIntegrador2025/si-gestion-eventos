using AutoMapper;
using DocumentFormat.OpenXml.InkML;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;
using System.Linq.Expressions;

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

        #region 1. Consultas (Queries)

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
                                    q => q.Cliente,
                                    q => q.Pagos
                                )).AsQueryable();

                if (fechaDesde.HasValue)
                    eventosQuery = eventosQuery.Where(e => e.Inicio.Date >= fechaDesde.Value.Date);

                if (fechaHasta.HasValue)
                    eventosQuery = eventosQuery.Where(e => e.Inicio.Date <= fechaHasta.Value.Date);

                if (estado.HasValue)
                    eventosQuery = eventosQuery.Where(e => e.Estado == estado.Value);

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

                // Calcular saldo y permisos para cada evento
                foreach (var eventoVM in items)
                {
                    var eventoEntity = eventosPaginados.FirstOrDefault(e => e.EventoId == eventoVM.EventoId);
                    if (eventoEntity != null)
                    {
                        // Calcular totales pagados (solo válidos)
                        decimal totalPagadoReal = eventoEntity.Pagos?
                                                        .Where(p => p.Valido)
                                                        .Sum(p => (decimal)p.Monto) ?? 0;

                        eventoVM.TotalPagado = totalPagadoReal;

                        decimal costoTotal = (decimal)eventoEntity.CostoAlquiler + (decimal)(eventoEntity.MontoAireAcondicionado ?? 0);
                        eventoVM.SaldoRestante = costoTotal - totalPagadoReal;

                        // Determinar si permite agregar pago
                        eventoVM.PermiteAgregarPago = DeterminarSiPermiteAgregarPago(eventoVM);
                    }
                }

                return new PaginatedList<EventoVM>(items, totalCount, page, pageSize);
            }
            catch (Exception)
            {
                return new PaginatedList<EventoVM>(new List<EventoVM>(), 0, page, pageSize);
            }
        }

        public async Task<EventoVM?> GetByIdAsync(int id)
        {
            var evento = await _eventoRepository.GetByIdWithIncludesAsync(id, e => e.Cliente, e => e.Pagos, e => e.Fianza);

            if (evento == null) return null;

            var eventoVM = _mapper.Map<EventoVM>(evento);
            decimal costoTotal = (decimal)(evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0));

            // Lógica de Saldos Reales (Filtrando Pagos Anulados)
            if (evento.Pagos != null && evento.Pagos.Any())
            {
                decimal totalPagadoReal = evento.Pagos
                                            .Where(p => p.Valido) // Solo sumamos válidos
                                            .Sum(p => (decimal)p.Monto);

                eventoVM.TotalPagado = (decimal)totalPagadoReal;

                costoTotal = (decimal)(evento.CostoAlquiler) + (decimal)(evento.MontoAireAcondicionado ?? 0);
                eventoVM.SaldoRestante = (decimal)costoTotal - eventoVM.TotalPagado;
            }
            else
            {
                eventoVM.TotalPagado = 0;
                eventoVM.SaldoRestante = costoTotal;
            }

            // Determinar si se permite agregar pago
            eventoVM.PermiteAgregarPago = DeterminarSiPermiteAgregarPago(eventoVM);

            // LÓGICA FECHA INDEFINIDA (Detectamos si está "estacionado" en el pasado lejano)
            if (evento.Estado == EventoEstado.Reprogramado)
            {
                // Si el año es menor a 2000, asumimos que es una fecha de "estacionamiento" (1900)
                eventoVM.EsFechaIndefinida = evento.Inicio.Year < 2000;
            }
            else
            {
                eventoVM.EsFechaIndefinida = false;
            }

            return eventoVM;
        }

        /// <summary>
        /// Determina si se permite agregar un pago a un evento según su estado y saldo.
        /// </summary>
        /// <param name="eventoVM">ViewModel del evento a evaluar</param>
        /// <returns>True si se permite agregar pago, False en caso contrario</returns>
        private bool DeterminarSiPermiteAgregarPago(EventoVM eventoVM)
        {
            // 1. NO SE PERMITE si el evento está CANCELADO
            if (eventoVM.Estado == EventoEstado.Cancelado)
                return false;

            // 2. NO SE PERMITE si el evento está REALIZADO (ya pasó)
            if (eventoVM.Estado == EventoEstado.Realizado)
                return false;

            // 3. NO SE PERMITE si está TOTALMENTE PAGADO (saldo <= 0)
            //    Esto aplica para CUALQUIER estado (Pendiente, Reprogramado, etc.)
            if (eventoVM.SaldoRestante <= 0)
                return false;

            // 4. NO SE PERMITE si está REPROGRAMADO y tiene fecha indefinida (año < 2000)
            //    Esto evita que se agreguen pagos a eventos que están "estacionados" esperando nueva fecha
            if (eventoVM.Estado == EventoEstado.Reprogramado && eventoVM.EsFechaIndefinida)
                return false;

            // 5. SÍ SE PERMITE en todos los demás casos:
            //    - PendientePagado (pero con saldo > 0, ej: pago parcial)
            //    - PendienteAdeudado
            //    - Reprogramado (con fecha definida y saldo > 0)
            return true;
        }

        // --- CreateAsync ---
        public async Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM)
        {
            var validationResult = await _validator.ValidateAsync(eventoVM, options => options.IncludeRuleSets("Create"));

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

            try
            {
                var evento = _mapper.Map<Evento>(eventoVM);
                evento.Estado = EventoEstado.PendienteAdeudado;

                await _eventoRepository.AddAsync(evento);
                await _eventoRepository.SaveChangesAsync();

                var eventoCreado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                var eventoVM_Creado = _mapper.Map<EventoVM>(eventoCreado);

                return ServiceResult<EventoVM>.SuccessResult(eventoVM_Creado, "Evento creado exitosamente.");
            }
            catch (Exception)
            {
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al crear el evento.");
            }
        }

        public async Task<ServiceResult<EventoVM>> CreateEventWithPaymentAsync(EventoVM eventoVM, PagoReservaVM pagoVM)
        {
            var validationResult = await _validator.ValidateAsync(eventoVM, options => options.IncludeRuleSets("Create"));
            if (!validationResult.IsValid)
            {
                return ServiceResult<EventoVM>.FailureResult(validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            bool isAvailable = await _businessRules.IsDateRangeAvailableAsync(
                eventoVM.Inicio, eventoVM.Fin, eventoVM.HoraInicio, eventoVM.HoraFin, null);

            if (!isAvailable)
                return ServiceResult<EventoVM>.FailureResult("El horario seleccionado ya no está disponible.");

            string urlComprobante = string.Empty;

            try
            {
                var evento = _mapper.Map<Evento>(eventoVM);
                float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));

                evento.Estado = pagoVM.Monto >= costoTotal ? EventoEstado.PendientePagado : EventoEstado.PendienteAdeudado;

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

                // Manejo de Comprobante
                if (pagoVM.ArchivoComprobante != null && pagoVM.ArchivoComprobante.Length > 0)
                {
                    urlComprobante = await _fileStorageService.GuardarArchivoAsync(pagoVM.ArchivoComprobante, "uploads/comprobantes");

                    if (string.IsNullOrEmpty(urlComprobante))
                        throw new InvalidOperationException("Error al guardar el archivo.");

                    var comprobante = new ComprobanteExterno
                    {
                        NombreArchivo = pagoVM.ArchivoComprobante.FileName,
                        RutaArchivo = urlComprobante,
                        FechaComprobante = DateTime.UtcNow,
                        TipoArchivo = ConvertExtensionToTipoArchivo(pagoVM.ArchivoComprobante.FileName),
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
                return ServiceResult<EventoVM>.SuccessResult(_mapper.Map<EventoVM>(eventoCreado), "Evento y pago creados exitosamente.");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(urlComprobante))
                    await _fileStorageService.BorrarArchivoAsync(urlComprobante);

                return ServiceResult<EventoVM>.FailureResult($"Ocurrió un error: {ex.Message}");
            }
        }

        public async Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM)
        {
            var validationResult = await _validator.ValidateAsync(eventoVM, options => options.IncludeRuleSets("default"));
            if (!validationResult.IsValid)
                return ServiceResult<EventoVM>.FailureResult(validationResult.Errors.Select(e => e.ErrorMessage).ToList());

            try
            {
                var evento = await _eventoRepository.GetByIdWithIncludesAsync(eventoVM.EventoId, e => e.Cliente);
                if (evento == null) return ServiceResult<EventoVM>.FailureResult("Evento no encontrado.");

                if (evento.Estado == EventoEstado.Cancelado || evento.Estado == EventoEstado.Realizado)
                    return ServiceResult<EventoVM>.FailureResult("No se puede editar un evento finalizado o cancelado.");

                if (!await _businessRules.IsClienteActiveAsync(evento.ClienteId))
                    return ServiceResult<EventoVM>.FailureResult("El cliente asociado está inactivo.");

                // Preservar datos inmutables
                var fechaContratoOriginal = evento.FechaContrato;
                var clienteIdOriginal = evento.ClienteId;

                // Aplicar cambios
                _mapper.Map(eventoVM, evento);

                // Restaurar inmutables
                evento.FechaContrato = fechaContratoOriginal;
                evento.ClienteId = clienteIdOriginal;

                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                var eventoActualizado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                return ServiceResult<EventoVM>.SuccessResult(_mapper.Map<EventoVM>(eventoActualizado), "Evento actualizado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<EventoVM>.FailureResult($"Error al actualizar: {ex.Message}");
            }
        }
        public async Task<List<EventoVM>> ObtenerTodosFiltradosAsync(string? q, DateTime? fechaDesde, DateTime? fechaHasta, EventoEstado? estado)
        {
            // Construimos el filtro (Predicado) en una sola expresión, igual que en PagoService
            Expression<Func<Evento, bool>> predicate = e =>
                (string.IsNullOrEmpty(q) ||
                 e.Cliente.Nombre.Contains(q) ||
                 e.Cliente.Apellido.Contains(q) ||
                 e.Cliente.CedulaIdentidad.Contains(q)) &&
                (!fechaDesde.HasValue || e.Inicio.Date >= fechaDesde.Value.Date) &&
                (!fechaHasta.HasValue || e.Inicio.Date <= fechaHasta.Value.Date) &&
                (!estado.HasValue || e.Estado == estado.Value);

            // Usamos el repositorio en lugar de _context
            // FindWithIncludesAsync trae los datos y las relaciones (Cliente)
            var listaEntidades = await _eventoRepository.FindWithIncludesAsync(predicate, e => e.Cliente);

            // Ordenamos en memoria (o el repositorio podría hacerlo si soporta OrderBy)
            listaEntidades = listaEntidades.OrderByDescending(e => e.Inicio).ToList();

            // Mapeamos a ViewModel
            return _mapper.Map<List<EventoVM>>(listaEntidades);
        }
        #endregion

        #region 3. Acciones de Negocio (Estado y Fechas)

        public async Task<ServiceResult<bool>> CancelAsync(int id)
        {
            try
            {
                if (!await _businessRules.CanCancelEventoAsync(id)) return ServiceResult<bool>.FailureResult("No se puede cancelar este evento.");

                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null) return ServiceResult<bool>.FailureResult("Evento no encontrado.");

                evento.Estado = EventoEstado.Cancelado;
                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido cancelado.");
            }
            catch (Exception) { return ServiceResult<bool>.FailureResult("Error inesperado al cancelar."); }
        }

        public async Task<ServiceResult<bool>> ConfirmAsync(int id)
        {
            try
            {
                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null) return ServiceResult<bool>.FailureResult("Evento no encontrado.");
                if (evento.Estado == EventoEstado.Cancelado) return ServiceResult<bool>.FailureResult("No se puede confirmar un evento cancelado.");
                if (evento.Estado == EventoEstado.PendienteAdeudado || evento.Estado == EventoEstado.PendientePagado)
                    return ServiceResult<bool>.FailureResult("El evento ya se encuentra confirmado.");
                if (!await _businessRules.IsEventoInFutureAsync(evento.Inicio)) return ServiceResult<bool>.FailureResult("No se puede confirmar un evento pasado.");

                evento.Estado = EventoEstado.PendienteAdeudado;
                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "Evento confirmado.");
            }
            catch (Exception) { return ServiceResult<bool>.FailureResult("Error inesperado al confirmar."); }
        }

        // --- REPROGRAMACIÓN SEGURA (Usa lógica de 1900) ---
        public async Task<ServiceResult<bool>> ReprogramarAsync(ReprogramarEventoVM model)
        {
            try
            {
                var evento = await _eventoRepository.GetByIdAsync(model.EventoId);
                if (evento == null) return ServiceResult<bool>.FailureResult("Evento no encontrado.");

                string fechaOriginal = evento.Inicio.ToString("dd/MM/yyyy HH:mm");
                string historial = $"[REPROGRAMADO]: Fecha original era {fechaOriginal}. Cambio el {DateTime.Now:dd/MM/yyyy}.";

                if (model.FechaIndefinida) historial += " (Pasado a fecha por definir).";

                if (!string.IsNullOrEmpty(evento.Observaciones))
                    evento.Observaciones += $"\n\n{historial}";
                else
                    evento.Observaciones = historial;

                if (model.FechaIndefinida)
                {
                    // ESTACIONAMIENTO INTELIGENTE (1900)
                    // Movemos al pasado manteniendo día/hora para evitar colisiones Unique Key
                    int aniosAtras = 1900 - evento.Inicio.Year;
                    evento.Inicio = evento.Inicio.AddYears(aniosAtras);
                    evento.Fin = evento.Fin.AddYears(aniosAtras);
                }
                else
                {
                    if (model.NuevaFecha.HasValue && model.NuevaHoraInicio.HasValue && model.NuevaHoraFin.HasValue)
                    {
                        evento.Inicio = model.NuevaFecha.Value.Date + model.NuevaHoraInicio.Value;
                        evento.Fin = model.NuevaFecha.Value.Date + model.NuevaHoraFin.Value;
                    }
                }

                evento.Estado = EventoEstado.Reprogramado;

                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "Evento reprogramado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Error al reprogramar: {ex.Message}");
            }
        }

        // [OBSOLETO] Este método es antiguo y no maneja la lógica de fechas indefinidas (1900).
        // Se recomienda eliminarlo y usar siempre ReprogramarAsync.
        /* public async Task<ServiceResult<bool>> RescheduleAsync(int id, DateTime nuevaFechaInicio, DateTime nuevaFechaFin, TimeSpan nuevaHoraInicio, TimeSpan nuevaHoraFin)
        {
             // Implementación antigua... mejor usar ReprogramarAsync
        } 
        */

        #endregion

        #region 4. Background Services (Servicios de Fondo)

        public async Task<List<EventoVM>> GetAlertasServiciosAsync()
        {
            var deadline = DateTime.Now.AddHours(48);
            var now = DateTime.Now;

            //Buscamos TODOS los eventos activos en la ventana de tiempo (48hs)
            
            var eventosProximos = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado != EventoEstado.Cancelado &&
                     e.Estado != EventoEstado.Realizado &&
                     e.Estado != EventoEstado.Reprogramado && // Ignorar reprogramados (año 1900)
                     e.Inicio > now &&
                     e.Inicio < deadline,
                e => e.Cliente,
                e => e.ServiciosEsenciales,
                e => e.Pagos);

            var listaAlertas = new List<EventoVM>();

            foreach (var evento in eventosProximos)
            {
                var motivosAlerta = new List<string>();

                //Verificar AGADU
                bool faltaAgadu = !evento.ServiciosEsenciales.OfType<CertificadoAGADU>().Any(c => c.Verificado);
                if (faltaAgadu)
                {
                    motivosAlerta.Add("Falta certificado AGADU");
                }

                //Verificar Deuda (RF-16: Verificar alquiler saldado)
                decimal costoTotal = (decimal)(evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0));

                // Sumar solo pagos válidos
                decimal totalPagado = evento.Pagos?
                                      .Where(p => p.Valido)
                                      .Sum(p => (decimal)p.Monto) ?? 0;

                decimal deuda = costoTotal - totalPagado;

                // Si debe algo (con pequeña tolerancia por decimales)
                if (deuda > 0.5m)
                {
                    motivosAlerta.Add($"Falta saldar ${deuda:N0}");
                }

                // C. Si falló alguna de las verificaciones, lo agregamos a la lista de alertas
                if (motivosAlerta.Any())
                {
                    var vm = _mapper.Map<EventoVM>(evento);

                    // Usamos el campo Observaciones del VM para mostrar la alerta en el Dashboard
                    vm.Observaciones = "ALERTA: " + string.Join(" + ", motivosAlerta);

                    listaAlertas.Add(vm);
                }
            }

            return listaAlertas;
        }

        public async Task<ServiceResult<int>> MarkCompletedEventsAsync()
        {
            int completedCount = 0;
            // FIX: Eliminamos 'EventoEstado.Reprogramado' de esta lista para no cerrar eventos en 1900
            var statesToComplete = new[] {
                EventoEstado.PendienteAdeudado,
                EventoEstado.PendientePagado,
            };

            try
            {
                var eventsToMark = await _eventoRepository.FindAsync(
                    e => e.Fin.Date < DateTime.Today &&
                         statesToComplete.Contains(e.Estado)
                );

                if (!eventsToMark.Any()) return ServiceResult<int>.SuccessResult(0, "No hay eventos para marcar.");

                foreach (var evento in eventsToMark)
                {
                    evento.Estado = EventoEstado.Realizado;
                    _eventoRepository.Update(evento);
                    completedCount++;
                }

                if (completedCount > 0) await _eventoRepository.SaveChangesAsync();

                return ServiceResult<int>.SuccessResult(completedCount, $"Se marcaron {completedCount} eventos como Realizados.");
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.FailureResult($"Error al marcar completados: {ex.Message}");
            }
        }

        

        public async Task<ServiceResult<int>> CheckAndCancelUnpaidEventsAsync()
        {
            // MÉTODO NEUTRALIZADO POR REGLA DE NEGOCIO (RF-16)
            // La documentación dice "Verificar y Alertar", NO cancelar automáticamente.
            // La lógica de alerta se ha movido a 'GetAlertasServiciosAsync'.
            // Mantenemos este método devolviendo 0 para no romper la interfaz ni los Background Workers.

            await Task.CompletedTask;
            return ServiceResult<int>.SuccessResult(0, "Cancelación automática desactivada.");
        }

        #endregion

        #region 5. Helpers y Dropdowns

        public async Task<IEnumerable<SelectListItem>> GetEventosSinFianzaParaDropdownAsync()
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado != EventoEstado.Cancelado && e.FianzaId == null,
                e => e.Cliente
            );

            if (eventos == null || !eventos.Any()) return new List<SelectListItem>();

            return eventos.OrderBy(e => e.Inicio).Select(e => new SelectListItem
            {
                Value = e.EventoId.ToString(),
                Text = $"{e.Cliente.Nombre} {e.Cliente.Apellido} - {e.Tipo} ({e.Inicio:dd/MM/yyyy})"
            });
        }

        public async Task<IEnumerable<SelectListItem>> GetTiposEventoParaDropdownAsync()
        {
            var tipos = Enum.GetValues<TipoEvento>().Select(t => new SelectListItem { Value = t.ToString(), Text = t.ToString() });
            return await Task.FromResult(tipos);
        }

        public async Task<IEnumerable<SelectListItem>> GetEventosAdeudadosParaDropdownAsync()
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado == EventoEstado.PendienteAdeudado,
                e => e.Cliente
            );

            if (eventos == null || !eventos.Any()) return new List<SelectListItem>();

            return eventos.OrderBy(e => e.Inicio).Select(e => new SelectListItem
            {
                Value = e.EventoId.ToString(),
                Text = $"{e.Cliente.Nombre} {e.Cliente?.Apellido} - {e.Tipo} - {e.Inicio:dd/MM/yyyy}"
            });
        }

        public async Task<bool> CanModifyEventoAsync(int eventoId)
        {
            return await _businessRules.CanModifyEventoAsync(eventoId);
        }

        public async Task<bool> HasConflictingEventsAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin, int? excludeEventoId = null)
        {
            return !await _businessRules.IsDateRangeAvailableAsync(inicio, fin, horaInicio, horaFin, excludeEventoId);
        }

        private TipoArchivo ConvertExtensionToTipoArchivo(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) throw new InvalidOperationException("Archivo sin nombre.");
            string ext = System.IO.Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".pdf" => TipoArchivo.PDF,
                ".png" => TipoArchivo.PNG,
                ".jpeg" => TipoArchivo.JPEG,
                ".jpg" => TipoArchivo.JPG,
                _ => throw new InvalidOperationException($"Tipo no permitido: {ext}")
            };
        }

        public async Task<List<EventoVM>> GetLatestAsync(int count)
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado != EventoEstado.Cancelado,
                e => e.Cliente,
                e => e.Pagos
            );

            var eventosOrdenados = eventos
                .OrderByDescending(e => e.Inicio)
                .Take(count)
                .ToList();

            return _mapper.Map<List<EventoVM>>(eventosOrdenados);
        }

        public async Task<IEnumerable<SelectListItem>> GetEventosParaFiltroPagosAsync()
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(
                e => true,
                e => e.Cliente
            );

            return eventos
                .OrderByDescending(e => e.Inicio)
                .Select(e => new SelectListItem
                {
                    Value = e.EventoId.ToString(),
                    Text = $"{(e.Cliente?.Nombre ?? "")} {(e.Cliente?.Apellido ?? "")} - {e.Tipo} ({e.Inicio:dd/MM/yyyy})"
                })
                .ToList();
        }


        #endregion
    }
}