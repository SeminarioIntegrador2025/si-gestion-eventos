using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController : Controller
    {
        // Inyectamos IEventoService y eliminamos AppDbContext
        // El controlador ya no debe saber cómo se guardan los datos.
        private readonly IEventoService _eventoService;
        private readonly IClienteService _clienteService;

        public EventoController(IEventoService eventoService, IClienteService clienteService)
        {
            _eventoService = eventoService;
            _clienteService = clienteService;
        }

        // GET: Evento
        public async Task<IActionResult> Index(string q, int page = 1, int pageSize = 10)
        {
            // La lógica de búsqueda, paginación y mapeo ahora vive en el servicio.
            var paginatedList = await _eventoService.GetAllPaginatedAsync(q, page, pageSize);

            ViewBag.Search = q;
            ViewBag.PageSize = pageSize;
            ViewBag.Ultimos = await _eventoService.GetLatestAsync(5); // Obtenemos los últimos del servicio.

            // La vista recibe directamente la lista de ViewModels paginada.
            return View(paginatedList);
        }

        // GET: Evento/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            // El servicio nos devuelve el ViewModel listo para la vista.
            var eventoVM = await _eventoService.GetByIdAsync(id);
            if (eventoVM is null)
            {
                return NotFound();
            }
            return View(eventoVM);
        }

        // GET: Evento/Create
        public async Task<IActionResult> Create()
        {
            // Pasamos un ViewModel vacío con valores por defecto a la vista.
            var viewModel = new EventoVM
            {
                FechaContrato = DateTime.Today,
                Inicio = DateTime.Today,
                Fin = DateTime.Today
            };

            await PopulateClientesDropdown();
            return View(viewModel);
        }

        // POST: Evento/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EventoVM eventoVM)
        {
            if (ModelState.IsValid)
            {
                var result = await _eventoService.CreateAsync(eventoVM);
                if (result.Success)
                {
                    TempData["Ok"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                // Agregamos los errores del servicio al ModelState.
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }

            await PopulateClientesDropdown();
            return View(eventoVM);
        }

        // GET: Evento/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var eventoVM = await _eventoService.GetByIdAsync(id);
            if (eventoVM is null)
            {
                return NotFound();
            }

            await PopulateClientesDropdown();

            return View(eventoVM);
        }
        // POST: Evento/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EventoVM eventoVM)
        {
            if (id != eventoVM.EventoId)
            {
                return NotFound();
            }

            // Primero, se valida que los datos del formulario cumplan las reglas básicas (ej: campos requeridos).
            if (ModelState.IsValid)
            {
                // Si los datos son válidos, intentamos actualizarlos usando el servicio.
                var result = await _eventoService.UpdateAsync(eventoVM);


                if (result.Success)
                {
                    TempData["Ok"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }
            await PopulateClientesDropdown();
            return View(eventoVM); 
        }

        // GET: Evento/Cancel/{id}
        public async Task<IActionResult> Cancel(int id)
        {
            var eventoVM = await _eventoService.GetByIdAsync(id);
            if (eventoVM is null)
            {
                return NotFound();
            }
            return View(eventoVM); 
        }

        // POST: Evento/Cancel/{id}
        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            var result = await _eventoService.CancelAsync(id);
            if (result.Success)
            {
                TempData["Ok"] = result.Message;
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors);
            }
            return RedirectToAction(nameof(Index));
        }

        // Método helper para no repetir el código de cargar el dropdown.
        private async Task PopulateClientesDropdown()
        {
            var clientes = await _clienteService.GetClientesActivosParaDropdownAsync();
            ViewBag.Clientes = new SelectList(clientes, "Value", "Text");
        }
    }
}