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

namespace si_td_gestion_eventos.Services.Implementation
{
    public class EventoService : IEventoService
    {
        private readonly IGenericRepository<Evento> _eventoRepository;
        private readonly IValidator<EventoVM> _validator;
        private readonly IEventoBusinessRules _businessRules;
        private readonly IMapper _mapper;

        public EventoService(
            IGenericRepository<Evento> eventoRepository,
            IValidator<EventoVM> validator,
            IEventoBusinessRules businessRules,
            IMapper mapper)
        {
            _eventoRepository = eventoRepository;
            _validator = validator;
            _businessRules = businessRules;
            _mapper = mapper;
        }

        public async Task<PaginatedList<EventoVM>> GetAllPaginatedAsync(
            string? searchQuery,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string ordenarPor,
            int page,
            int pageSize,
            bool incluirPasados = false,
            bool incluirCancelados = false)
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

                // Filtrar eventos pasados si no se solicita incluirlos
                if (!incluirPasados)
                {
                    var hoy = DateTime.Today;
                    eventosQuery = eventosQuery.Where(e => e.Inicio.Date >= hoy);
                }

                // Filtrar eventos cancelados si no se solicita incluirlos
                if (!incluirCancelados)
                {
                    eventosQuery = eventosQuery.Where(e => e.Estado != EventoEstado.Cancelado);
                }

                if (fechaDesde.HasValue)
                {
                    eventosQuery = eventosQuery.Where(e => e.Inicio.Date >= fechaDesde.Value.Date);
                }
                if (fechaHasta.HasValue)
                {
                    eventosQuery = eventosQuery.Where(e => e.Inicio.Date <= fechaHasta.Value.Date);
                }

                IOrderedQueryable<Evento> eventosOrdenados = ordenarPor switch
                {
                    "fecha_inicio_desc" => eventosQuery.OrderByDescending(e => e.Inicio),
                    "fecha_contrato_asc" => eventosQuery.OrderBy(e => e.FechaContrato),
                    "fecha_contrato_desc" => eventosQuery.OrderByDescending(e => e.FechaContrato),
                    "cantidad_personas_desc" => eventosQuery.OrderByDescending(e => e.CantidadPersonas),
                    "cantidad_personas_asc" => eventosQuery.OrderBy(e => e.CantidadPersonas),
                    "tipo_evento" => eventosQuery.OrderBy(e => e.Tipo),
                    _ => eventosQuery.OrderBy(e => e.Inicio) // Por defecto: más actual primero
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

        public async Task<EventoVM?> GetByIdAsync(int id)
        {
            var evento = await _eventoRepository.GetByIdWithIncludesAsync(id, e => e.Cliente);
            return evento != null ? _mapper.Map<EventoVM>(evento) : null;
        }

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

            try
            {
                var evento = _mapper.Map<Evento>(eventoVM);
                evento.Estado = EventoEstado.Pendiente;

                await _eventoRepository.AddAsync(evento);
                await _eventoRepository.SaveChangesAsync();

                var eventoCreado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                var eventoVM_Creado = _mapper.Map<EventoVM>(eventoCreado);

                return ServiceResult<EventoVM>.SuccessResult(eventoVM_Creado, "Evento creado exitosamente.");
            }
            catch (Exception ex)
            {
                // Loggear error
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al crear el evento.");
            }
        }

        public async Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM)
        {
            // Validar usando SOLO las reglas comunes (sin el RuleSet "Create")
            var validationResult = await _validator.ValidateAsync(eventoVM, options =>
                options.IncludeRuleSets("default"));

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<EventoVM>.FailureResult(errors);
            }

            try
            {
                var evento = await _eventoRepository.GetByIdWithIncludesAsync(eventoVM.EventoId, e => e.Cliente);
                if (evento == null)
                {
                    return ServiceResult<EventoVM>.FailureResult("Evento no encontrado.");
                }

                // Verificar si el cliente está activo
                if (!await _businessRules.IsClienteActiveAsync(evento.ClienteId))
                {
                    return ServiceResult<EventoVM>.FailureResult("No se puede modificar este evento porque el cliente está inactivo.");
                }

                // Verificar si se puede modificar el evento
                if (!await _businessRules.CanModifyEventoAsync(eventoVM.EventoId))
                {
                    return ServiceResult<EventoVM>.FailureResult("No se puede modificar este evento. Debe tener al menos 48 horas de anticipación y no estar cancelado.");
                }

                // Preservar valores que no se deben cambiar
                var fechaContratoOriginal = evento.FechaContrato;
                var clienteIdOriginal = evento.ClienteId;
                var costoAlquilerOriginal = evento.CostoAlquiler;
                var montoReservaOriginal = evento.MontoReserva;

                // Mapear los cambios
                _mapper.Map(eventoVM, evento);

                // Restaurar valores que no se deben cambiar
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
            catch (Exception ex)
            {
                // Loggear error
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al actualizar el evento.");
            }
        }

        public async Task<ServiceResult<bool>> CancelAsync(int id)
        {
            try
            {
                if (!await _businessRules.CanCancelEventoAsync(id))
                {
                    return ServiceResult<bool>.FailureResult("No se puede cancelar este evento. El evento debe ser futuro y no estar ya cancelado.");
                }

                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null)
                {
                    return ServiceResult<bool>.FailureResult("Evento no encontrado.");
                }

                evento.Estado = EventoEstado.Cancelado;
                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido cancelado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al cancelar el evento.");
            }
        }

        public async Task<ServiceResult<bool>> RescheduleAsync(int id, DateTime nuevaFechaInicio, DateTime nuevaFechaFin, TimeSpan nuevaHoraInicio, TimeSpan nuevaHoraFin)
        {
            try
            {
                if (!await _businessRules.CanRescheduleEventoAsync(id))
                {
                    return ServiceResult<bool>.FailureResult("No se puede reprogramar este evento. Debe tener al menos 48 horas de anticipación y no estar cancelado.");
                }

                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null)
                {
                    return ServiceResult<bool>.FailureResult("Evento no encontrado.");
                }

                if (!await _businessRules.IsValidDateRangeAsync(nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin))
                {
                    return ServiceResult<bool>.FailureResult("El nuevo rango de fechas y horarios no es válido.");
                }

                if (!await _businessRules.IsDateRangeAvailableAsync(nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin, id))
                {
                    return ServiceResult<bool>.FailureResult("Ya existe otro evento programado en el nuevo horario seleccionado.");
                }

                evento.Inicio = nuevaFechaInicio;
                evento.Fin = nuevaFechaFin;
                evento.HoraInicio = nuevaHoraInicio;
                evento.HoraFin = nuevaHoraFin;
                evento.Estado = EventoEstado.Reprogramado;

                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido reprogramado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al reprogramar el evento.");
            }
        }

        public async Task<ServiceResult<bool>> ConfirmAsync(int id)
        {
            try
            {
                var evento = await _eventoRepository.GetByIdAsync(id);
                if (evento == null)
                {
                    return ServiceResult<bool>.FailureResult("Evento no encontrado.");
                }

                if (evento.Estado == EventoEstado.Cancelado)
                {
                    return ServiceResult<bool>.FailureResult("No se puede confirmar un evento cancelado.");
                }

                if (evento.Estado == EventoEstado.Pendiente)
                {
                    return ServiceResult<bool>.FailureResult("El evento ya se encuentra pendiente.");
                }

                if (!await _businessRules.IsEventoInFutureAsync(evento.Inicio))
                {
                    return ServiceResult<bool>.FailureResult("No se puede confirmar un evento que ya pasó.");
                }

                evento.Estado = EventoEstado.Pendiente;
                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "El evento ha sido confirmado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al confirmar el evento.");
            }
        }

        public async Task<List<EventoVM>> GetLatestAsync(int count)
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(
                null, e => e.Cliente,
                e => e.Cliente
            );

            var eventosOrdenados = eventos.OrderByDescending(e => e.EventoId).Take(count);
            return _mapper.Map<List<EventoVM>>(eventosOrdenados);
        }

        public async Task<IEnumerable<SelectListItem>> GetTiposEventoParaDropdownAsync()
        {
            var tiposEvento = Enum.GetValues<TipoEvento>()
                .Select(tipo => new SelectListItem
                {
                    Value = tipo.ToString(),
                    Text = tipo.ToString()
                });

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

        public async Task<IEnumerable<SelectListItem>> GetEventosActivosParaDropdownAsync()
        {

            var eventosActivos = await _eventoRepository.FindWithIncludesAsync(

                e => e.Estado == EventoEstado.Pendiente, //AGREGAR TODOS LOS OTROS ESTADOS || e.Estado == EventoEstado.Pendiente, 
                e => e.Cliente
            );

            if (eventosActivos == null || !eventosActivos.Any())
            {
                return new List<SelectListItem>();
            }

            return eventosActivos
                .OrderBy(e => e.Inicio)
                .Select(e => new SelectListItem
                {
                    Value = e.EventoId.ToString(),
                    Text = $"{e.Cliente.Nombre} {e.Cliente?.Apellido} - {e.Tipo} - {e.Inicio:dd/MM/yyyy}"
                });
        }
    }
};
