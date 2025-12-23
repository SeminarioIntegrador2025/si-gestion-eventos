using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
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
    
        private readonly AppDbContext _context;

        public EventoService(
            IGenericRepository<Evento> eventoRepository,
            IGenericRepository<Pago> pagoRepository,
            IValidator<EventoVM> validator,
            IEventoBusinessRules businessRules,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IGenericRepository<ComprobanteExterno> comprobanteRepository,
            AppDbContext context)
        {
            _pagoRepository = pagoRepository;
            _eventoRepository = eventoRepository;
            _validator = validator;
            _businessRules = businessRules;
            _mapper = mapper;
            _fileStorageService = fileStorageService;
            _comprobanteRepository = comprobanteRepository;
            _context = context;
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
                string queryOriginal = searchQuery?.Trim().ToLower() ?? "";
                string queryNumbers = searchQuery?.Replace(".", "").Replace("-", "").Trim() ?? "";

                Expression<Func<Evento, bool>> textPredicate = e =>
                    string.IsNullOrEmpty(queryOriginal) ||
                    e.Cliente.Nombre.ToLower().Contains(queryOriginal) ||
                    (e.Cliente.Apellido != null && e.Cliente.Apellido.ToLower().Contains(queryOriginal)) ||
                    (e.Cliente.Nombre + " " + (e.Cliente.Apellido ?? "")).ToLower().Contains(queryOriginal) ||
                    e.Cliente.CedulaIdentidad.Replace(".", "").Replace("-", "").Contains(queryNumbers) ||
                    e.Tipo.ToString().ToLower().Contains(queryOriginal);

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

                foreach (var eventoVM in items)
                {
                    var eventoEntity = eventosPaginados.FirstOrDefault(e => e.EventoId == eventoVM.EventoId);
                    if (eventoEntity != null)
                    {
                        decimal totalPagadoReal = eventoEntity.Pagos?
                                                        .Where(p => p.Valido)
                                                        .Sum(p => (decimal)p.Monto) ?? 0;

                        eventoVM.TotalPagado = totalPagadoReal;
                        decimal costoTotal = (decimal)eventoEntity.CostoAlquiler + (decimal)(eventoEntity.MontoAireAcondicionado ?? 0);
                        eventoVM.SaldoRestante = costoTotal - totalPagadoReal;
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

            if (evento.Pagos != null && evento.Pagos.Any())
            {
                decimal totalPagadoReal = evento.Pagos.Where(p => p.Valido).Sum(p => (decimal)p.Monto);
                eventoVM.TotalPagado = totalPagadoReal;
                eventoVM.SaldoRestante = costoTotal - eventoVM.TotalPagado;
            }
            else
            {
                eventoVM.TotalPagado = 0;
                eventoVM.SaldoRestante = costoTotal;
            }

            eventoVM.PermiteAgregarPago = DeterminarSiPermiteAgregarPago(eventoVM);
            eventoVM.EsFechaIndefinida = evento.Estado == EventoEstado.Reprogramado && evento.Inicio.Year < 2000;

            return eventoVM;
        }

        private bool DeterminarSiPermiteAgregarPago(EventoVM eventoVM)
        {
            if (eventoVM.Estado == EventoEstado.Cancelado || eventoVM.Estado == EventoEstado.Realizado) return false;
            if (eventoVM.SaldoRestante <= 0) return false;
            if (eventoVM.Estado == EventoEstado.Reprogramado && eventoVM.EsFechaIndefinida) return false;
            return true;
        }

        public async Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM)
        {
            var validationResult = await _validator.ValidateAsync(eventoVM, options => options.IncludeRuleSets("Create"));

            if (!validationResult.IsValid)
            {
                return ServiceResult<EventoVM>.FailureResult(validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            if (!await _businessRules.IsDateRangeAvailableAsync(eventoVM.Inicio, eventoVM.Fin, eventoVM.HoraInicio, eventoVM.HoraFin, null))
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
                return ServiceResult<EventoVM>.SuccessResult(_mapper.Map<EventoVM>(eventoCreado), "Evento creado exitosamente.");
            }
            catch (Exception)
            {
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al crear el evento.");
            }
        }

        // --- MÉTODO CORREGIDO CON TRANSACCIÓN Y MIEMBROS REQUERIDOS ---
        public async Task<ServiceResult<EventoVM>> CreateEventWithPaymentAsync(EventoVM eventoVM, PagoReservaVM pagoVM)
        {
            var validationResult = await _validator.ValidateAsync(eventoVM, options => options.IncludeRuleSets("Create"));
            if (!validationResult.IsValid)
            {
                return ServiceResult<EventoVM>.FailureResult(validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            // CORRECCIÓN CS0103: Ahora _context es accesible gracias a la inyección en el constructor
            using var transaction = await _context.Database.BeginTransactionAsync();
            string urlComprobante = string.Empty;

            try
            {
                if (!await _businessRules.IsDateRangeAvailableAsync(eventoVM.Inicio, eventoVM.Fin, eventoVM.HoraInicio, eventoVM.HoraFin, null))
                    return ServiceResult<EventoVM>.FailureResult("El horario seleccionado ya no está disponible.");

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
                    Metodo = pagoVM.Metodo,
                    Valido = true
                };

                await _pagoRepository.AddAsync(pago);
                await _pagoRepository.SaveChangesAsync();

                if (pagoVM.ArchivoComprobante != null && pagoVM.ArchivoComprobante.Length > 0)
                {
                    urlComprobante = await _fileStorageService.GuardarArchivoAsync(pagoVM.ArchivoComprobante, "uploads/comprobantes");

                    if (string.IsNullOrEmpty(urlComprobante))
                        throw new InvalidOperationException("Error al guardar el archivo físico.");

                    var comprobante = new ComprobanteExterno
                    {
                        NombreArchivo = pagoVM.ArchivoComprobante.FileName,
                        RutaArchivo = urlComprobante,
                        FechaComprobante = DateTime.UtcNow,
                        TipoArchivo = ConvertExtensionToTipoArchivo(pagoVM.ArchivoComprobante.FileName),
                        PagoId = pago.PagoId,
                        // CORRECCIÓN CS9035: Se asigna el miembro requerido 'Pago'
                        Pago = pago
                    };

                    await _comprobanteRepository.AddAsync(comprobante);
                    await _comprobanteRepository.SaveChangesAsync();

                    pago.ComprobanteExternoId = comprobante.ComprobanteExternoId;
                    _pagoRepository.Update(pago);
                    await _pagoRepository.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                var eventoCreado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                return ServiceResult<EventoVM>.SuccessResult(_mapper.Map<EventoVM>(eventoCreado), "Evento y pago creados exitosamente.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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

                var fechaContratoOriginal = evento.FechaContrato;
                var clienteIdOriginal = evento.ClienteId;

                _mapper.Map(eventoVM, evento);

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
            string searchRaw = q?.Replace(".", "").Replace("-", "").Trim().ToLower() ?? "";
            string searchOriginal = q?.Trim().ToLower() ?? "";

            Expression<Func<Evento, bool>> predicate = e =>
                (string.IsNullOrEmpty(searchOriginal) ||
                 e.Cliente.Nombre.ToLower().Contains(searchOriginal) ||
                 (e.Cliente.Apellido != null && e.Cliente.Apellido.ToLower().Contains(searchOriginal)) ||
                 (e.Cliente.Nombre + " " + (e.Cliente.Apellido ?? "")).ToLower().Contains(searchOriginal) ||
                 e.Cliente.CedulaIdentidad.Replace(".", "").Replace("-", "").Contains(searchRaw)) &&
                (!fechaDesde.HasValue || e.Inicio.Date >= fechaDesde.Value.Date) &&
                (!fechaHasta.HasValue || e.Inicio.Date <= fechaHasta.Value.Date) &&
                (!estado.HasValue || e.Estado == estado.Value);

    var listaEntidades = await _eventoRepository.FindWithIncludesAsync(
        predicate, 
        e => e.Cliente, 
        e => e.Pagos  // ← ESTE ES EL CAMBIO CLAVE
    );
    
    var listaOrdenada = listaEntidades.OrderByDescending(e => e.Inicio).ToList();
    var items = _mapper.Map<List<EventoVM>>(listaOrdenada);

    foreach (var eventoVM in items)
    {
        var eventoEntity = listaOrdenada.FirstOrDefault(e => e.EventoId == eventoVM.EventoId);
        if (eventoEntity != null)
        {
            // Calculamos el total de pagos válidos
            decimal totalPagadoReal = eventoEntity.Pagos?
                                            .Where(p => p.Valido)
                                            .Sum(p => (decimal)p.Monto) ?? 0;

            eventoVM.TotalPagado = totalPagadoReal;

            // Calculamos el costo total y el saldo restante
            decimal costoTotal = (decimal)eventoEntity.CostoAlquiler + (decimal)(eventoEntity.MontoAireAcondicionado ?? 0);
            eventoVM.SaldoRestante = costoTotal - totalPagadoReal;
        }
    }

    return items;
}
        #endregion

        #region 3. Acciones de Negocio (Estado y Fechas)

        public async Task<ServiceResult<bool>> CancelAsync(int id)
        {
            try
            {
                if (!await _businessRules.CanCancelEventoAsync(id)) 
                    return ServiceResult<bool>.FailureResult("No se puede cancelar este evento.");

                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null) 
                    return ServiceResult<bool>.FailureResult("Evento no encontrado.");

                var estadoAnterior = evento.Estado;
                var fechaOriginal = evento.Inicio.Year > 2000 
                    ? evento.Inicio.ToString("dd/MM/yyyy HH:mm") 
                    : "Fecha por definir";

                // Cambiar estado
                evento.Estado = EventoEstado.Cancelado;

                string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                string mensajeAuditoria = $"[EVENTO CANCELADO] Estado anterior: '{estadoAnterior}'. Fecha programada: {fechaOriginal}. Cancelado el {fechaHoy}.";
                
                if (string.IsNullOrEmpty(evento.Observaciones))
                    evento.Observaciones = mensajeAuditoria;
                else
                    evento.Observaciones += $"{Environment.NewLine}{mensajeAuditoria}";

                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido cancelado y se registró en las observaciones.");
            }
            catch (Exception ex) 
            { 
                return ServiceResult<bool>.FailureResult($"Error inesperado al cancelar: {ex.Message}"); 
            }
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

        public async Task<ServiceResult<bool>> ReprogramarAsync(ReprogramarEventoVM model)
        {
            try
            {
                var evento = await _eventoRepository.GetByIdAsync(model.EventoId);
                if (evento == null) return ServiceResult<bool>.FailureResult("No existe el evento.");

                // VALIDACIÓN: No permitir reprogramar eventos finalizados
                if (evento.Estado == EventoEstado.Realizado || evento.Estado == EventoEstado.Cancelado)
                {
                    return ServiceResult<bool>.FailureResult($"No se puede reprogramar un evento en estado '{evento.Estado}'. Los eventos realizados o cancelados no pueden ser reprogramados.");
                }

                // 1. DETECCIÓN DE ESTADO POR FECHA (Lógica Centinela)
                // Verificamos si la fecha guardada es el "Flag" de 1900
                bool estabaIndefinido = evento.Inicio.Year == 1900;

                // 2. SNAPSHOT HISTÓRICO
                string fechaOriginalStr = estabaIndefinido
                    ? "Fecha por definir"
                    : evento.Inicio.ToString("dd/MM/yyyy");

                string mensajeAuditoria = "";
                string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                // 3. APLICACIÓN DE CAMBIOS
                if (model.FechaIndefinida)
                {
                    // --- CASO A: Pasa a Indefinido ---
                    evento.Estado = EventoEstado.Reprogramado;
                    evento.Inicio = new DateTime(1900, 1, 1);
                    evento.Fin = new DateTime(1900, 1, 1);

                    mensajeAuditoria = $"[REPROGRAMADO] Fecha original: {fechaOriginalStr}. Pasado a fecha por definir el {fechaHoy}.";
                }
                else if (model.NuevaFechaInicio.HasValue && model.NuevaFechaFin.HasValue &&
                         model.NuevaHoraInicio.HasValue && model.NuevaHoraFin.HasValue)
                {
                    // --- CASO B: Pasa a Fecha Concreta ---
                    if (!await _businessRules.IsDateRangeAvailableAsync(
                        model.NuevaFechaInicio.Value, model.NuevaFechaFin.Value,
                        model.NuevaHoraInicio.Value, model.NuevaHoraFin.Value, model.EventoId))
                    {
                        return ServiceResult<bool>.FailureResult("El salón ya está ocupado en ese nuevo horario.");
                    }

                    evento.Estado = EventoEstado.Reprogramado;
                    evento.Inicio = model.NuevaFechaInicio.Value.Date + model.NuevaHoraInicio.Value;
                    evento.Fin = model.NuevaFechaFin.Value.Date + model.NuevaHoraFin.Value;
                    evento.HoraInicio = model.NuevaHoraInicio.Value;
                    evento.HoraFin = model.NuevaHoraFin.Value;

                    string nuevaFechaStr = evento.Inicio.ToString("dd/MM/yyyy HH:mm");
                    mensajeAuditoria = $"[REPROGRAMADO] Fecha original: {fechaOriginalStr}. Nueva fecha: {nuevaFechaStr}. Modificado el {fechaHoy}.";
                }
                else
                {
                    return ServiceResult<bool>.FailureResult("Faltan datos para reprogramar.");
                }

                // 4. ACTUALIZACIÓN DE OBSERVACIONES
                if (string.IsNullOrEmpty(evento.Observaciones))
                    evento.Observaciones = mensajeAuditoria;
                else
                    evento.Observaciones += $"{Environment.NewLine}{mensajeAuditoria}";

                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "Evento reprogramado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Error: {ex.Message}");
            }
        }

        #endregion

        #region 4. Background Services

        public async Task<List<EventoVM>> GetAlertasServiciosAsync()
        {
            var deadline = DateTime.Now.AddHours(48);
            var hoy = DateTime.Today; // 00:00 de hoy

            // CONSULTA HÍBRIDA (Pasado + Futuro)
            var eventosProblema = await _eventoRepository.FindWithIncludesAsync(
                e => e.Estado != EventoEstado.Cancelado &&
                     e.Estado != EventoEstado.Realizado &&
                     (
                        // GRUPO 1: FUTURO INMEDIATO (Próximas 48hs)
                        // Eventos que van a ocurrir pronto (o hoy)
                        (e.Inicio >= hoy && e.Inicio < deadline)
                        ||
                        // GRUPO 2: PASADO IRREGULAR (Deudas viejas)
                        // CORRECCIÓN AQUI: Agregamos '&& e.Inicio.Year > 2000'
                        // Esto evita que los eventos con fecha "1900" (Reprogramados sin fecha)
                        // salgan como "Evento Finalizado" y generen alerta.
                        (e.Fin < DateTime.Now && e.Estado == EventoEstado.PendienteAdeudado && e.Inicio.Year > 2000)
                     ),
                e => e.Cliente,
                e => e.Pagos,
                e => e.ServiciosEsenciales
            );

            var listaAlertas = new List<EventoVM>();

            foreach (var eventoEntity in eventosProblema)
            {
                var vm = _mapper.Map<EventoVM>(eventoEntity);
                var alertasEncontradas = new List<string>();

                // Lógica de Deuda
                decimal totalPagado = eventoEntity.Pagos?.Where(p => p.Valido).Sum(p => (decimal)p.Monto) ?? 0;
                decimal costoTotal = (decimal)(eventoEntity.CostoAlquiler + (eventoEntity.MontoAireAcondicionado ?? 0));
                decimal deuda = costoTotal - totalPagado;
                bool yaPaso = eventoEntity.Fin < DateTime.Now;

                // MENSAJES PERSONALIZADOS
                if (deuda > 10)
                {
                    if (yaPaso)
                    {
                        // MENSAJE ESPECIAL PARA "CASO B" (Pasado)
                        alertasEncontradas.Add($"¡EVENTO FINALIZADO! Falta pagar ${deuda:N0}");
                    }
                    else
                    {
                        // MENSAJE NORMAL (Futuro)
                        alertasEncontradas.Add($"DEUDA: Falta saldar ${deuda:N0}");
                    }
                }

                // Lógica de Servicios (Solo si es futuro, porque si ya pasó, ya no importa tanto verificar servicios)
                if (!yaPaso && vm.ServiciosEsenciales != null && vm.ServiciosEsenciales.Any(s => !s.Verificado))
                {
                    alertasEncontradas.Add("SERVICIOS: Faltan verificar");
                }

                // Si encontramos algo, lo agregamos
                if (alertasEncontradas.Any())
                {
                    vm.Observaciones = string.Join(" | ", alertasEncontradas);
                    listaAlertas.Add(vm);
                }
            }

            return listaAlertas;
        }

        public async Task<ServiceResult<int>> ActualizarEstadosEventosPasadosAsync()
        {
            try
            {
                var hoy = DateTime.Now;

                // 1. OBTENER CANDIDATOS:
                // Buscamos TODOS los eventos que ya terminaron (Fin < hoy)
                // y que no han sido cerrados definitivamente (no son ni Realizado ni Cancelado).
                var eventosPasados = await _eventoRepository.FindWithIncludesAsync(
                    e => e.Fin < hoy &&
                         e.Estado != EventoEstado.Realizado &&
                         e.Estado != EventoEstado.Cancelado,
                    e => e.Pagos // Traemos pagos para hacer la matemática financiera
                );

                int contadorModificados = 0;

                foreach (var evento in eventosPasados)
                {
                    // 2. CALCULO FINANCIERO EXACTO
                    decimal totalPagado = evento.Pagos?.Where(p => p.Valido).Sum(p => (decimal)p.Monto) ?? 0;
                    decimal costoTotal = (decimal)(evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0));
                    decimal saldo = costoTotal - totalPagado;

                    // 3. TOMA DE DECISIONES

                    // CASO A: Evento finalizado y PAGADO (Saldo ~0)
                    if (saldo <= 10)
                    {
                        if (evento.Estado != EventoEstado.Realizado)
                        {
                            evento.Estado = EventoEstado.Realizado;
                            // Opcional: Log interno
                            // evento.Observaciones += " | [SISTEMA] Cerrado automáticamente por pago completo.";
                            _eventoRepository.Update(evento);
                            contadorModificados++;
                        }
                    }
                    // CASO B: Evento finalizado pero DEBE DINERO
                    // Lo marcamos como 'PendienteAdeudado' para que salga en las Alertas Rojas y KPIs de deuda
                    else
                    {
                        if (evento.Estado != EventoEstado.PendienteAdeudado)
                        {
                            evento.Estado = EventoEstado.PendienteAdeudado;
                            // No lo cancelamos, solo marcamos la deuda para gestión
                            _eventoRepository.Update(evento);
                            contadorModificados++;
                        }
                    }
                }

                // 4. GUARDAR CAMBIOS EN LOTE
                if (contadorModificados > 0)
                {
                    await _eventoRepository.SaveChangesAsync();
                }

                return ServiceResult<int>.SuccessResult(contadorModificados, $"Se actualizaron {contadorModificados} eventos pasados.");
            }
            catch (Exception ex)
            {
                return ServiceResult<int>.FailureResult(ex.Message);
            }
        }

        #endregion

        #region 5. Helpers

        public async Task<ServiceResult<bool>> CambiarEstadoManualAsync(int id, EventoEstado nuevoEstado)
        {
            var evento = await _eventoRepository.GetByIdAsync(id);
            if (evento == null) return ServiceResult<bool>.FailureResult("Evento no encontrado.");
            
            // Guardar el estado anterior para auditoría
            var estadoAnterior = evento.Estado;
            
            // Cambiar el estado
            evento.Estado = nuevoEstado;
            
            // Registrar el cambio en observaciones con timestamp
            string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            string mensajeAuditoria = $"[CAMBIO MANUAL] Estado cambiado de '{estadoAnterior}' a '{nuevoEstado}' el {fechaHoy}.";
            
            if (string.IsNullOrEmpty(evento.Observaciones))
                evento.Observaciones = mensajeAuditoria;
            else
                evento.Observaciones += $"{Environment.NewLine}{mensajeAuditoria}";
            
            _eventoRepository.Update(evento);
            await _eventoRepository.SaveChangesAsync();
            
            return ServiceResult<bool>.SuccessResult(true, $"Estado actualizado a '{nuevoEstado}' correctamente.");
        }

        public async Task<IEnumerable<SelectListItem>> GetEventosSinFianzaParaDropdownAsync()
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(e => e.Estado != EventoEstado.Cancelado && e.FianzaId == null, e => e.Cliente);
            return eventos.Select(e => new SelectListItem { Value = e.EventoId.ToString(), Text = $"{e.Cliente.Nombre} {(e.Cliente.Apellido ?? "")} - {e.Tipo} - Fecha de Inicio: {e.Inicio:dd/MM}" });
        }

        public async Task<IEnumerable<SelectListItem>> GetTiposEventoParaDropdownAsync() => Enum.GetValues<TipoEvento>().Select(t => new SelectListItem { Value = t.ToString(), Text = t.ToString() });

        public async Task<IEnumerable<SelectListItem>> GetEventosAdeudadosParaDropdownAsync()
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(e => e.Estado == EventoEstado.PendienteAdeudado, e => e.Cliente);
            return eventos.Select(e => new SelectListItem { Value = e.EventoId.ToString(), Text = $"{e.Cliente.Nombre}  {(e.Cliente.Apellido ?? "")} - Fecha de Inicio: {e.Inicio:dd/MM}" });
        }

        public async Task<bool> CanModifyEventoAsync(int id) => await _businessRules.CanModifyEventoAsync(id);

        public async Task<bool> HasConflictingEventsAsync(DateTime i, DateTime f, TimeSpan hi, TimeSpan hf, int? id = null) => !await _businessRules.IsDateRangeAvailableAsync(i, f, hi, hf, id);

        private TipoArchivo ConvertExtensionToTipoArchivo(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName).ToLower();
            return ext switch { ".pdf" => TipoArchivo.PDF, ".png" => TipoArchivo.PNG, ".jpeg" => TipoArchivo.JPEG, ".jpg" => TipoArchivo.JPG, _ => throw new Exception("Extensión no válida") };
        }

        public async Task<List<EventoVM>> GetLatestAsync(int count)
        {
            var evs = await _eventoRepository.FindWithIncludesAsync(e => e.Estado != EventoEstado.Cancelado, e => e.Cliente);
            return _mapper.Map<List<EventoVM>>(evs.OrderByDescending(e => e.Inicio).Take(count));
        }

        public async Task<IEnumerable<SelectListItem>> GetEventosParaFiltroPagosAsync()
        {
            var evs = await _eventoRepository.FindWithIncludesAsync(e => true, e => e.Cliente);
            return evs.OrderByDescending(e => e.Inicio).Select(e => new SelectListItem { Value = e.EventoId.ToString(), Text = $"{e.Cliente.Nombre}  {(e.Cliente.Apellido ?? "")} - Fecha de Inicio: {e.Inicio:dd/MM}" });
        }

    }

    #endregion
}
