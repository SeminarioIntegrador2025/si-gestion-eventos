using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Services.Implementation // Se recomienda la carpeta "Implementation"
{
    public class EventoService : IEventoService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly IValidator<EventoVM> _validator;

        // Inyectamos AutoMapper y el Validador para mantener la consistencia con ClienteService
        public EventoService(AppDbContext context, IMapper mapper, IValidator<EventoVM> validator)
        {
            _context = context;
            _mapper = mapper;
            _validator = validator;
        }

        public async Task<PaginatedList<EventoVM>> GetAllPaginatedAsync(string? searchQuery, int page, int pageSize)
        {
            var query = _context.Evento
                .AsNoTracking()
                .Include(e => e.Cliente)
                .OrderByDescending(e => e.Inicio)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(e =>
                    e.Cliente.Nombre.Contains(searchQuery) ||
                    e.Cliente.Apellido.Contains(searchQuery) ||
                    e.Tipo.ToString().Contains(searchQuery));
            }

            // Usamos la proyección de AutoMapper para convertir la consulta a EventoVM
            var vmQuery = _mapper.ProjectTo<EventoVM>(query);

            return await PaginatedList<EventoVM>.CreateAsync(vmQuery, page, pageSize);
        }

        public async Task<EventoVM?> GetByIdAsync(int id)
        {
            var evento = await _context.Evento
                .AsNoTracking()
                .Include(e => e.Cliente)
                .Include(e => e.Pagos)
                .FirstOrDefaultAsync(e => e.EventoId == id);

           
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

                // Asignar valores por defecto que no vienen del formulario
                evento.Estado = EventoEstado.PendienteAConfirmar;

                _context.Evento.Add(evento);
                await _context.SaveChangesAsync();

                // Actualizar el ID en el VM y devolverlo
                eventoVM.EventoId = evento.EventoId;
                return ServiceResult<EventoVM>.SuccessResult(eventoVM, "Evento creado exitosamente.");
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
                var evento = await _context.Evento.FindAsync(eventoVM.EventoId);
                if (evento == null)
                {
                    return ServiceResult<EventoVM>.FailureResult("Evento no encontrado.");
                }

                // Mapear los cambios desde el VM a la entidad existente
                _mapper.Map(eventoVM, evento);

                _context.Update(evento);
                await _context.SaveChangesAsync();

                return ServiceResult<EventoVM>.SuccessResult(eventoVM, "Evento actualizado correctamente.");
            }
            catch (Exception ex)
            {
                // Loguear el error
                return ServiceResult<EventoVM>.FailureResult("Ocurrió un error inesperado al actualizar el evento.");
            }
        }

        public async Task<ServiceResult<bool>> CancelAsync(int id)
        {
            var evento = await _context.Evento.FindAsync(id);
            if (evento == null)
            {
                return ServiceResult<bool>.FailureResult("Evento no encontrado.");
            }
            if (evento.Estado == EventoEstado.Cancelado)
            {
                return ServiceResult<bool>.FailureResult("El evento ya se encuentra cancelado.");
            }

            evento.Estado = EventoEstado.Cancelado;
            await _context.SaveChangesAsync();

            return ServiceResult<bool>.SuccessResult(true, "El evento ha sido cancelado.");
        }

        public async Task<List<EventoVM>> GetLatestAsync(int count)
        {
            var eventos = await _context.Evento
                .AsNoTracking()
                .Include(e => e.Cliente)
                .OrderByDescending(e => e.EventoId)
                .Take(count)
                .ToListAsync();

            return _mapper.Map<List<EventoVM>>(eventos);
        }
    }
}