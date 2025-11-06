using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System; 
using System.Collections.Generic; 
using System.Linq; 
using System.Threading.Tasks; 

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

        // GET: Evento/Index
        public async Task<IActionResult> Index(
            string q,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EventoEstado? estado,
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

                ViewBag.EstadosList = new SelectList(Enum.GetValues(typeof(EventoEstado)), estado);
                ViewBag.PageSizeList = new SelectList(new[] { 10, 25, 50 }, pageSize);
                ViewBag.OrdenPorList = new SelectList(GetOrdenPorOpciones(), "Value", "Text", ordenPor);

                var emptyList = new PaginatedList<EventoVM>(new List<EventoVM>(), 0, page, pageSize);
                return View(emptyList);
            }
            var paginatedList = await _eventoService.GetAllPaginatedAsync(
                q,
                fechaDesde,
                fechaHasta,
                estado,
                ordenPor,
                page,
                pageSize);
            ViewBag.Search = q;
            ViewBag.FechaDesde = fechaDesde;
            ViewBag.FechaHasta = fechaHasta;
            ViewBag.Estado = estado; 

            // --- Listas para los Dropdowns ---
            ViewBag.EstadosList = new SelectList(Enum.GetValues(typeof(EventoEstado)), estado);
            ViewBag.PageSizeList = new SelectList(new[] { 10, 25, 50 }, pageSize);
            ViewBag.OrdenPorList = new SelectList(GetOrdenPorOpciones(), "Value", "Text", ordenPor);

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
            // 1. Validar con FluentValidation (reglas de formato, fechas lógicas, etc.)
            var validationResult = await _validator.ValidateAsync(eventoVM, options =>
            {
                options.IncludeRuleSets("default", "Create");
            });
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
            }

            // 2. Calcular la lógica de 48hs (para las sugerencias)
            var deadline = DateTime.Now.AddHours(48);
            var now = DateTime.Now;
            DateTime fechaInicioReal = eventoVM.Inicio.Date.Add(eventoVM.HoraInicio);
            bool esDentroDe48Hs = (fechaInicioReal < deadline && fechaInicioReal > now);

            // 3. Preparar las "Sugerencias" para el Paso 2
            string observacionSugerida;
            float montoSugerido;

            if (esDentroDe48Hs)
            {
                // Si es < 48hs, SIEMPRE sugerimos el pago total
                observacionSugerida = "Pago completo del salón (Evento < 48hs)";
                float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));
                montoSugerido = costoTotal;
            }
            else
            {
                // Si es lejano, sugerimos la reserva normal
                observacionSugerida = "Por Reserva";
                montoSugerido = (float)eventoVM.MontoReserva;
            }

            // 4. Comprobar el ModelState (SOLO de FluentValidation)
            if (ModelState.IsValid)
            {
                // 5. Redirigir al Paso 2, pasando las SUGERENCIAS
                TempData["PendingEvent"] = JsonConvert.SerializeObject(eventoVM);

                var pagoVm = new PagoReservaVM
                {
                    Monto = montoSugerido, // <-- Pasa el monto sugerido
                    Fecha = eventoVM.FechaContrato,
                    Observaciones = observacionSugerida // <-- Pasa la observación sugerida
                };
                TempData["PendingPaymentDetails"] = JsonConvert.SerializeObject(pagoVm);

                return RedirectToAction("CreateReserva", "Pago");
            }

            // Si FluentValidation falló (ej. faltó un cliente)
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

            var validationResult = await _validator.ValidateAsync(eventoVM, options =>
            {
                options.IncludeRuleSets("default", "Edit");
            });

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

        // --- Métodos Helper ---

        private async Task PopulateClientesDropdown()
        {
            var clientes = await _clienteService.GetClientesActivosParaDropdownAsync();
            ViewBag.Clientes = new SelectList(clientes, "Value", "Text");
        }

        private List<SelectListItem> GetOrdenPorOpciones()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "fecha_inicio_asc", Text = "Fecha de inicio (más cercana)" },
                new SelectListItem { Value = "fecha_inicio_desc", Text = "Fecha de inicio (más lejana)" },
                new SelectListItem { Value = "fecha_contrato_asc", Text = "Fecha de contrato (más cercana)" },
                new SelectListItem { Value = "fecha_contrato_desc", Text = "Fecha de contrato (más lejana)" },
                new SelectListItem { Value = "cantidad_personas_desc", Text = "Cantidad de Personas (Mayor)" },
                new SelectListItem { Value = "cantidad_personas_asc", Text = "Cantidad de Personas (Menor)" },
                new SelectListItem { Value = "tipo_evento", Text = "Tipo de Evento (A-Z)" }
            };
        }
    }
}