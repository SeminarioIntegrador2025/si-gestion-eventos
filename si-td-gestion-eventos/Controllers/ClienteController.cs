using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Entities;
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
            var clienteVM = await _clienteService.GetByIdAsync(id);
            if (clienteVM is null) return NotFound();
            return View(clienteVM);
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
            ViewBag.Message = null;
            if (!ModelState.IsValid) return View(clienteVM);

            try
            {
                await _clienteService.AddAsync(clienteVM);
                ViewBag.Message = "Cliente creado con éxito.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
                return View(clienteVM);
            }
        }

        // GET: Cliente/EditAsync/{idCliente}
        [HttpGet]
        public async Task<IActionResult> EditAsync(int? id)
        {
            var clienteVM = await _clienteService.GetByIdAsync(id.Value);
            return View(clienteVM);
        }

        // POST: Cliente/EditAsync/{idCliente}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAsync(ClienteVM clienteVM)
        {           
            if (!ModelState.IsValid)
            {
                return View(clienteVM);
            }

            try
            {
                await _clienteService.EditAsync(clienteVM);
                TempData["Message"] = "Cliente actualizado con éxito.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al actualizar: " + ex.Message);
                return View(clienteVM);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUnactive(int id)
        {
            try
            {
                var clienteVM = await _clienteService.GetByIdAsync(id);
                if (clienteVM != null)
                {
                    clienteVM.Activo = false;
                    await _clienteService.EditAsync(clienteVM);
                    TempData["Message"] = "Se ha dado de baja correctamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al dar de baja: " + ex.Message;
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var clienteVM = await _clienteService.GetByIdAsync(id);
                if (clienteVM != null)
                {
                    clienteVM.Activo = true;
                    await _clienteService.EditAsync(clienteVM);
                    TempData["Message"] = "Se ha dado de alta correctamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al dar de alta: " + ex.Message;
            }
            return RedirectToAction("Details", new { id });
        }
    }
}
