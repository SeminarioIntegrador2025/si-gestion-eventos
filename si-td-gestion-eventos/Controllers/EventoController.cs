using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using FluentValidation;

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController : Controller
    {
        private readonly IEventoService _eventoService;
        private readonly IClienteService _clienteService;
        private readonly IValidator<EventoVM> _validator;

        public EventoController(
            IEventoService eventoService, 
            IClienteService clienteService,
            IValidator<EventoVM> validator)
        {
            _eventoService = eventoService;
            _clienteService = clienteService;
            _validator = validator;
        }

        // GET: Evento/Index (eventos actuales)
        public async Task<IActionResult> Index(
            string q,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string ordenPor = "fecha_inicio_asc",
            int page = 1,
            int pageSize = 10)
        {     
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value < fechaDesde.Value)
            {
                ModelState.AddModelError("Fechas", "La 'Fecha Hasta' no puede ser anterior a la 'Fecha Desde'.");                
                ViewBag.Search = q;
                ViewBag.FechaDesde = fechaDesde;
                ViewBag.FechaHasta = fechaHasta;
                ViewBag.OrdenPor = ordenPor;
                ViewBag.PageSize = pageSize;
                var emptyList = new PaginatedList<EventoVM>(new List<EventoVM>(), 0, page, pageSize);
                return View(emptyList);
            }

            var paginatedList = await _eventoService.GetAllPaginatedAsync(
                q,
                fechaDesde,
                fechaHasta,
                ordenPor,
                page,
                pageSize,
                incluirPasados: false,
                incluirCancelados: true);

            ViewBag.Search = q;
            ViewBag.FechaDesde = fechaDesde;
            ViewBag.FechaHasta = fechaHasta;
            ViewBag.PageSize = pageSize;
            ViewBag.Ultimos = await _eventoService.GetLatestAsync(5);
            ViewBag.OrdenPor = ordenPor;
            return View(paginatedList);
        }

        // GET: Evento/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
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
            var viewModel = new EventoVM
            {
                FechaContrato = DateTime.Today,
                Inicio = DateTime.Today.AddDays(1),
                Fin = DateTime.Today.AddDays(1)
            };

            await PopulateClientesDropdown();
            return View(viewModel);
        }

        // POST: Evento/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EventoVM eventoVM)
        {
            // IMPORTANTE: Validar con FluentValidation incluyendo reglas comunes + RuleSet "Create"
            var validationResult = await _validator.ValidateAsync(eventoVM, options => 
            {
                options.IncludeRuleSets("default", "Create", "Edit");
            });

            // Agregar errores de validación al ModelState manualmente
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
            }

            if (ModelState.IsValid)
            {
                var result = await _eventoService.CreateAsync(eventoVM);
                if (result.Success)
                {
                    TempData["Ok"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                // Agregar los errores del servicio al ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }

            await PopulateClientesDropdown();
            return View(eventoVM);
        }

        // GET: Evento/Historial (eventos pasados)
        public async Task<IActionResult> Historial(
            string q,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string ordenPor = "fecha_inicio_desc",
            int page = 1,
            int pageSize = 10)
        {
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value < fechaDesde.Value)
            {
                ModelState.AddModelError("Fechas", "La 'Fecha Hasta' no puede ser anterior a la 'Fecha Desde'.");
                ViewBag.Search = q;
                ViewBag.FechaDesde = fechaDesde;
                ViewBag.FechaHasta = fechaHasta;
                ViewBag.OrdenPor = ordenPor;
                ViewBag.PageSize = pageSize;
                var emptyList = new PaginatedList<EventoVM>(new List<EventoVM>(), 0, page, pageSize);
                return View("Index", emptyList);
            }

            var paginatedList = await _eventoService.GetAllPaginatedAsync(
                q,
                fechaDesde,
                fechaHasta,
                ordenPor,
                page,
                pageSize,
                incluirPasados: true,
                incluirCancelados: false);

            ViewBag.Search = q;
            ViewBag.FechaDesde = fechaDesde;
            ViewBag.FechaHasta = fechaHasta;
            ViewBag.PageSize = pageSize;
            ViewBag.OrdenPor = ordenPor;
            ViewBag.TituloVista = "Historial de Eventos";
            return View("Index", paginatedList);
        }

        // GET: Evento/Cancelados
        public async Task<IActionResult> Cancelados(
            string q,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string ordenPor = "fecha_inicio_desc",
            int page = 1,
            int pageSize = 10)
        {
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value < fechaDesde.Value)
            {
                ModelState.AddModelError("Fechas", "La 'Fecha Hasta' no puede ser anterior a la 'Fecha Desde'.");
                ViewBag.Search = q;
                ViewBag.FechaDesde = fechaDesde;
                ViewBag.FechaHasta = fechaHasta;
                ViewBag.OrdenPor = ordenPor;
                ViewBag.PageSize = pageSize;
                var emptyList = new PaginatedList<EventoVM>(new List<EventoVM>(), 0, page, pageSize);
                return View("Index", emptyList);
            }

            var paginatedList = await _eventoService.GetAllPaginatedAsync(
                q,
                fechaDesde,
                fechaHasta,
                ordenPor,
                page,
                pageSize,
                incluirPasados: true,
                incluirCancelados: true);

            var eventosCancelados = paginatedList.Where(e => e.Estado == Models.Enums.EventoEstado.Cancelado).ToList();
            var totalCancelados = eventosCancelados.Count;
            var result = new PaginatedList<EventoVM>(eventosCancelados, totalCancelados, page, pageSize);

            ViewBag.Search = q;
            ViewBag.FechaDesde = fechaDesde;
            ViewBag.FechaHasta = fechaHasta;
            ViewBag.PageSize = pageSize;
            ViewBag.OrdenPor = ordenPor;
            ViewBag.TituloVista = "Eventos Cancelados";
            return View("Index", result);
        }

        // GET: Evento/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var eventoVM = await _eventoService.GetByIdAsync(id);
            if (eventoVM is null)
            {
                return NotFound();
            }

            var canModify = await _eventoService.CanModifyEventoAsync(id);
            ViewBag.CanModify = canModify;

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

            // Validar con FluentValidation incluyendo reglas comunes + RuleSet "Edit"
            var validationResult = await _validator.ValidateAsync(eventoVM, options => 
            {
                options.IncludeRuleSets("default", "Edit");
            });

            // Agregar errores de validación al ModelState manualmente
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
            }

            if (ModelState.IsValid)
            {
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
            
            // IMPORTANTE: Recargar los datos del evento desde la BD para recuperar ClienteNombreCompleto
            // Si hay errores de validación, necesitamos mantener los datos del cliente
            if (!ModelState.IsValid)
            {
                var eventoOriginal = await _eventoService.GetByIdAsync(id);
                if (eventoOriginal != null)
                {
                    eventoVM.ClienteNombreCompleto = eventoOriginal.ClienteNombreCompleto;
                    eventoVM.ClienteId = eventoOriginal.ClienteId;
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

        private async Task PopulateClientesDropdown()
        {
            var clientes = await _clienteService.GetClientesActivosParaDropdownAsync();
            ViewBag.Clientes = new SelectList(clientes, "Value", "Text");
        }
    }
}