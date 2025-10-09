// En Services/ClienteService.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Context;

namespace si_td_gestion_eventos.Services
{
    public class ClienteService : IClienteService
    {
        private readonly AppDbContext _dbContext;
    
        public ClienteService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public SelectList GetClientesActivosParaDropdown()
        {
            var clientesDisponibles = _dbContext.Cliente
                                              .Where(c => c.Activo)
                                              .OrderBy(c => c.Apellido)
                                              .ThenBy(c => c.Nombre)
                                               .Select(c => new
                                               {
                                                   ClienteId = c.ClienteId,
                                                   NombreCompleto = c.Apellido + ", " + c.Nombre + ", CI: " + c.CedulaIdentidad // Concatenamos aquí
                                               })
                .ToList();

            return new SelectList(clientesDisponibles, "ClienteId", "NombreCompleto");
        }
    }
}