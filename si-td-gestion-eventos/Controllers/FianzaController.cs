using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Context;

namespace si_td_gestion_eventos.Controllers
{
    public class FianzaController(AppDbContext _dbContext) : Controller
    {
        public IActionResult Index()
        {
            var fianzas = _dbContext.Fianza.ToList();
            return View(fianzas);
        }
    }
}
