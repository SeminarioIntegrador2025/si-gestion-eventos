using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController(AppDbContext _dbContext) : Controller
    {
        public IActionResult Index()
        {
            var eventos = _dbContext.Evento
                .Include(e => e.Cliente) 
                .ToList();
            return View(eventos);
        }


        // GET: Evento/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Evento/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Evento evento)
        {
            if (ModelState.IsValid)
            {
                _dbContext.Evento.Add(evento);
                _dbContext.SaveChanges();
                return RedirectToAction(nameof(Index));

            }
      
            return View(evento);
        }

    }
}
