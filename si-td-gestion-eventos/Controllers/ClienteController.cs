using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services;

namespace si_td_gestion_eventos.Controllers
{
    public class ClienteController(ClienteService _clienteService) : Controller
    {
        // GET: Cliente
        public async Task<IActionResult> Index()
        {
            var clientes = await _clienteService.GetAllAsync();
            return View(clientes.ToList());
        }
         
        // GET: Cliente/Details/{idCliente}
        public async Task<IActionResult> Details(int id)
        {
            //var cliente = await _clienteService.GetByIdAsync(id);
            //if (cliente is null) return NotFound();
            return View();//cliente);
        }

        // GET: Cliente/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Cliente/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteVM clienteVM)
        {
            if (!ModelState.IsValid) return View(clienteVM);

            try
            {
                await _clienteService.AddAsync(clienteVM);
                TempData["message"] = "Cliente creado correctamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
                return View(clienteVM);
            }
        }

        // GET: Cliente/Edit/{idCliente}
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            //if (id is null) return NotFound();
            //var cliente = await _clienteService.GetByIdAsync(id.Value);
            //if (cliente is null) return NotFound();
            return View();//cliente);
        }

        
    }
}
