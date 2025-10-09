using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;

namespace si_td_gestion_eventos.Controllers
{
    public class PagoController(AppDbContext _dbContext) : Controller
    {
        public IActionResult Index()
        {
            var pagos = _dbContext.Pago
                .Include(p => p.Evento)
                .ToList();
            return View(pagos);
        }

        // GET: Pago/Create        
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Eventos = _dbContext.Evento.ToList();
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Pago pago)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Eventos = _dbContext.Evento.ToList();
                return View(pago);
            }

            // Procesar el pago
            // Guardar en la base de datos, etc.

            return RedirectToAction("Index");
        }

    }
}

