using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class ClienteBusinessRules : IClienteBusinessRules
    {
        private readonly AppDbContext _context;

        public ClienteBusinessRules(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsCedulaUniqueAsync(string cedula, int? excludeClienteId = null)
        {
            var query = _context.Cliente.Where(c => c.CedulaIdentidad == cedula);

            if (excludeClienteId.HasValue)
            {
                query = query.Where(c => c.ClienteId != excludeClienteId.Value);
            }

            return !await query.AnyAsync();
        }

        public async Task<bool> CanDeactivateClienteAsync(int clienteId)
        {
            // Verificar si el cliente tiene eventos activos
            return !await HasActiveEventsAsync(clienteId);
        }

        public async Task<bool> HasActiveEventsAsync(int clienteId)
        {
            return await _context.Evento
                .AnyAsync(e => e.ClienteId == clienteId &&
                              e.FechaInicio > DateTime.Now);
        }
    }
}