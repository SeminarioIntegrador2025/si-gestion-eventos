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
        public IEnumerable<SelectListItem> GetClientesActivosParaDropdown()
        {
            return _dbContext.Cliente 
                .Where(c => c.Activo)
                .OrderBy(c => c.Apellido)
                .ThenBy(c => c.Nombre)
                .Select(c => new SelectListItem
                {                  
                    Value = c.ClienteId.ToString(),
                    Text = $"{c.Apellido}, {c.Nombre} (CI: {c.CedulaIdentidad})"
                })
                .ToList();
        }
    }
}