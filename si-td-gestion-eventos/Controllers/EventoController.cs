using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Context;

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController(AppDbContext _dbContext) : Controller
    {
        public IActionResult Index()
        {
            var eventos = _dbContext.Evento.ToList();
            return View(eventos);
        }
    }
}
