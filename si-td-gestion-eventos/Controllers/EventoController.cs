using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Services; 

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly IClienteService _clienteService; // Inyectamos el servicio

        public EventoController(AppDbContext dbContext, IClienteService clienteService)
        {
            _dbContext = dbContext;
            _clienteService = clienteService;
        }

        public IActionResult Index()
        {
            var eventos = _dbContext.Evento
                .Include(e => e.Cliente) // Solo si necesitas mostrar datos del cliente
                .ToList();
            return View(eventos);
        }

        // GET: Evento/Create
        public IActionResult Create()
        {
            ViewBag.Clientes = _clienteService.GetClientesActivosParaDropdown();
            return View();
        }

        // POST: Evento/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Evento evento)
        {
            if (ModelState.IsValid)
            {
                // Usamos el nombre en plural para el DbSet
                _dbContext.Evento.Add(evento);
                _dbContext.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            // Si el modelo NO es válido, recargamos el ViewBag antes de devolver la vista
            ViewBag.Clientes = _clienteService.GetClientesActivosParaDropdown();
            return View(evento);
        }
    }
}