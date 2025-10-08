using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;

namespace si_td_gestion_eventos.Controllers
{
    public class ClienteController(AppDbContext _dbContext) : Controller
    {
        public IActionResult Index()
        {
            var clientes = _dbContext.Cliente.ToList();
            return View(clientes);
        }

        // GET: Cliente/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Cliente/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Cliente cliente)
        {
            if (ModelState.IsValid)
            {
                _dbContext.Cliente.Add(cliente);
                _dbContext.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }
    }
}
