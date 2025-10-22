using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class EventoBusinessRules : IEventoBusinessRules
    {
        private readonly AppDbContext _context;

        public EventoBusinessRules(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsDateRangeAvailableAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin, int? excludeEventoId = null)
        {
            var query = _context.Evento.AsNoTracking().Where(e => e.Estado != EventoEstado.Cancelado);

            if (excludeEventoId.HasValue)
            {
                query = query.Where(e => e.EventoId != excludeEventoId.Value);
            }

            return !await query.AnyAsync(e => 
                // Verificar solapamiento de fechas
                (inicio.Date <= e.Fin.Date && fin.Date >= e.Inicio.Date) &&
                // Si es el mismo día, verificar solapamiento de horarios
                (inicio.Date != e.Inicio.Date || (horaInicio < e.HoraFin && horaFin > e.HoraInicio)) &&
                (fin.Date != e.Fin.Date || (horaInicio < e.HoraFin && horaFin > e.HoraInicio))
            );


            
        }

        public async Task<bool> CanCancelEventoAsync(int eventoId)
        {
            var evento = await _context.Evento.FirstOrDefaultAsync(e => e.EventoId == eventoId);
            if (evento == null) return false;
            if (evento.Estado == EventoEstado.Cancelado) return false;
            return evento.Inicio > DateTime.Now;
        }

        public async Task<bool> CanModifyEventoAsync(int eventoId)
        {
            var evento = await _context.Evento
                .FirstOrDefaultAsync(e => e.EventoId == eventoId);

            if (evento == null) return false;

            // No se puede modificar si está cancelado o ya pasó
            return evento.Estado != EventoEstado.Cancelado && 
                   evento.Inicio > DateTime.Now.AddHours(48); 
        }

        public async Task<bool> HasPaymentsAsync(int eventoId)
        {
            return await _context.Evento
                .Where(e => e.EventoId == eventoId)
                .SelectMany(e => e.Pagos)
                .AnyAsync();
        }

        public async Task<bool> HasFianzaAsync(int eventoId)
        {
            return await _context.Fianza
                .AnyAsync(f => f.EventoId == eventoId);
        }

        public async Task<bool> IsClienteActiveAsync(int clienteId)
        {
            return await _context.Cliente
                .AnyAsync(c => c.ClienteId == clienteId && c.Activo);
        }

        public async Task<bool> IsEventoInFutureAsync(DateTime inicio)
        {
            return inicio > DateTime.Now;
        }

        public async Task<bool> IsValidDateRangeAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin)
        {
            // Verificar que la fecha de fin no sea anterior a la de inicio
            if (fin.Date < inicio.Date) return false;

            // Si es el mismo día, verificar que la hora de fin sea posterior a la de inicio
            if (inicio.Date == fin.Date && horaFin <= horaInicio) return false;

            return await Task.FromResult(true);
        }

        public async Task<bool> IsReservationAmountValidAsync(decimal montoReserva, decimal costoAlquiler)
        {
            return await Task.FromResult(montoReserva >= 0 && montoReserva <= costoAlquiler);
        }

        public async Task<bool> CanRescheduleEventoAsync(int eventoId)
        {
            var evento = await _context.Evento
                .FirstOrDefaultAsync(e => e.EventoId == eventoId);

            if (evento == null) return false;

            // Se puede reprogramar si no está cancelado y es en el futuro
            return evento.Estado != EventoEstado.Cancelado && 
                   evento.Inicio > DateTime.Now.AddHours(48); // Al menos 48 horas de anticipación
        }

        public async Task<int> GetActiveEventsCountForClienteAsync(int clienteId)
        {
            return await _context.Evento
                .CountAsync(e => e.ClienteId == clienteId && 
                               e.Estado != EventoEstado.Cancelado &&
                               e.Inicio > DateTime.Now);
        }
    }
}