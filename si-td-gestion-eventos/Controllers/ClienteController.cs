using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Controllers
{
    public class ClienteController : Controller
    {
        private readonly IClienteService _clienteService;

        public ClienteController(IClienteService clienteService)
        {
            _clienteService = clienteService;
        }

        // GET: Cliente
        public async Task<IActionResult> Index()
        {
            var clientes = await _clienteService.GetAllAsync();
            return View(clientes.ToList());
        }

        // GET: Cliente/Details/{idCliente}
        public async Task<IActionResult> Details(int id)
        {
            var clienteVM = await _clienteService.GetByIdAsync(id);
            if (clienteVM is null) return NotFound();
            return View(clienteVM);
        }

        // GET: Cliente/Create
        [HttpGet]
        public IActionResult Create()
        {
            var clienteVM = new ClienteVM
            {
                Tipo = si_td_gestion_eventos.Models.Enums.TipoCliente.PersonaFisica,
                Activo = true,
                Nombre = string.Empty,
                Domicilio = string.Empty,
                Telefono = string.Empty,
            };
            return View(clienteVM);
        }

        // POST: Cliente/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteVM clienteVM)
        {
            var result = await _clienteService.CreateAsync(clienteVM);

            if (result.Success)
            {
                TempData["Message"] = result.Message;
                return RedirectToAction("Index");
            }
            
            // Agregar errores de FluentValidation al ModelState
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            
            return View(clienteVM);
        }

        // GET: Cliente/EditAsync/{idCliente}
        [HttpGet]
        public async Task<IActionResult> EditAsync(int? id)
        {
            if (!id.HasValue) return NotFound();

            var clienteVM = await _clienteService.GetByIdAsync(id.Value);
            if (clienteVM == null) return NotFound();

            return View(clienteVM);
        }

        // POST: Cliente/EditAsync/{idCliente}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAsync(ClienteVM clienteVM)
        {
            
            var result = await _clienteService.UpdateAsync(clienteVM);

            if (result.Success)
            {
                TempData["Message"] = result.Message;
                return RedirectToAction("Index");
            }

            // Agregar errores de validación al ModelState
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(clienteVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUnactive(int id)
        {
            var result = await _clienteService.DeactivateAsync(id);
            
            if (result.Success)
            {
                TempData["Message"] = result.Message;
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors);
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var result = await _clienteService.ActivateAsync(id);
            
            if (result.Success)
            {
                TempData["Message"] = result.Message;
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors);
            }

            return RedirectToAction("Details", new { id });
        }
    }
}
