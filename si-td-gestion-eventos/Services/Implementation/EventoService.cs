using AutoMapper;
using FluentValidation;
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
        private readonly IMapper _mapper;
        private readonly IValidator<EventoVM> _validator;

        public EventoService(
            IGenericRepository<Evento> eventoRepository,
            IMapper mapper,
            IValidator<EventoVM> validator)
        {
            _eventoRepository = eventoRepository;
            _mapper = mapper;
            _validator = validator;
        }

        public async Task<PaginatedList<EventoVM>> GetAllPaginatedAsync(string? searchQuery, int page, int pageSize)
        {
            try
            {
                // Obtener eventos con la relación Cliente incluida
                var eventos = await _eventoRepository.FindWithIncludesAsync(
                    predicate: e =>
                        string.IsNullOrEmpty(searchQuery) ||
                        e.Cliente.Nombre.Contains(searchQuery) ||
                        e.Cliente.Apellido.Contains(searchQuery) ||
                        e.Cliente.CedulaIdentidad.Contains(searchQuery) ||
                        e.ResponsableNombre.Contains(searchQuery) ||
                        e.Tipo.ToString().Contains(searchQuery),
                    includes: e => e.Cliente // Incluir la entidad Cliente
                );

                // Ordenar por fecha de inicio descendente
                var eventosOrdenados = eventos.OrderByDescending(e => e.Inicio);

                // Mapear a ViewModels
                var eventosVM = _mapper.Map<IEnumerable<EventoVM>>(eventosOrdenados);

                // Crear lista paginada
                var totalCount = eventosVM.Count();
                var items = eventosVM.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return new PaginatedList<EventoVM>(items, totalCount, page, pageSize);
            }
            catch (Exception ex)
            {
                // Manejar el error apropiadamente (log, rethrow, etc.)
                throw new Exception("Error al obtener los eventos.", ex);
            }
        }

        public async Task<EventoVM?> GetByIdAsync(int id)
        {
            // Obtener evento con Cliente incluido
            var evento = await _eventoRepository.GetByIdWithIncludesAsync(id, e => e.Cliente);
            return evento != null ? _mapper.Map<EventoVM>(evento) : null;
        }

        public async Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM)
        {
            // Validar el ViewModel
            var validationResult = await _validator.ValidateAsync(eventoVM);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<EventoVM>.FailureResult(errors);
            }

            try
            {
                // Mapear ViewModel a Entidad
                var evento = _mapper.Map<Evento>(eventoVM);

                // Asignar valores por defecto
                evento.Estado = EventoEstado.PendienteAConfirmar;

                await _eventoRepository.AddAsync(evento);
                await _eventoRepository.SaveChangesAsync();

                // Obtener el evento creado con Cliente para devolver información completa
                var eventoCreado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                var eventoVM_Creado = _mapper.Map<EventoVM>(eventoCreado);

                return ServiceResult<EventoVM>.SuccessResult(eventoVM_Creado, "Evento creado exitosamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al crear el evento.");
            }
        }

        public async Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM)
        {
            // Validar el ViewModel
            var validationResult = await _validator.ValidateAsync(eventoVM);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<EventoVM>.FailureResult(errors);
            }

            try
            {
                var evento = await _eventoRepository.GetByIdAsync(eventoVM.EventoId);
                if (evento == null)
                {
                    return ServiceResult<EventoVM>.FailureResult("Evento no encontrado.");
                }

                // Mapear los cambios desde el VM a la entidad existente
                _mapper.Map(eventoVM, evento);

                _eventoRepository.Update(evento);
                await _eventoRepository.SaveChangesAsync();

                // Obtener el evento actualizado con Cliente para devolver información completa
                var eventoActualizado = await _eventoRepository.GetByIdWithIncludesAsync(evento.EventoId, e => e.Cliente);
                var eventoVM_Actualizado = _mapper.Map<EventoVM>(eventoActualizado);

                return ServiceResult<EventoVM>.SuccessResult(eventoVM_Actualizado, "Evento actualizado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al actualizar el evento.");
            }
        }

        public async Task<ServiceResult<bool>> CancelAsync(int id)
        {
            var evento = await _eventoRepository.GetByIdAsync(id);
            if (evento == null)
            {
                return ServiceResult<bool>.FailureResult("Evento no encontrado.");
            }

            if (evento.Estado == EventoEstado.Cancelado)
            {
                return ServiceResult<bool>.FailureResult("El evento ya se encuentra cancelado.");
            }

            evento.Estado = EventoEstado.Cancelado;
            _eventoRepository.Update(evento);
            await _eventoRepository.SaveChangesAsync();

            return ServiceResult<bool>.SuccessResult(true, "El evento ha sido cancelado.");
        }

        public async Task<List<EventoVM>> GetLatestAsync(int count)
        {
            var eventos = await _eventoRepository.FindWithIncludesAsync(
                predicate: null, 
                includes: e => e.Cliente
            );

            var eventosOrdenados = eventos.OrderByDescending(e => e.EventoId).Take(count);
            return _mapper.Map<List<EventoVM>>(eventosOrdenados);
        }
    }
}