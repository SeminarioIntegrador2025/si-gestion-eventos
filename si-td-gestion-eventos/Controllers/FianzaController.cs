using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System.Threading.Tasks;

namespace si_td_gestion_eventos.Controllers
{
    public class FianzaController : Controller
    {
        private readonly IFianzaService _fianzaService;
        private readonly IEventoService _eventoService;

        public FianzaController(IFianzaService fianzaService, IEventoService eventoService)
        {
            _fianzaService = fianzaService;
            _eventoService = eventoService;
        }

        //Listado
        [HttpGet]
        public async Task<IActionResult> Index(string? q, EstadoFianza? estado, int page = 1, int pageSize = 10)
        {
            var paginatedList = await _fianzaService.GetPaginatedAsync(q, estado, page, pageSize);
            ViewBag.Search = q;
            ViewBag.Estado = estado;
            ViewBag.EstadosList = new SelectList(Enum.GetValues(typeof(EstadoFianza)), estado);
            ViewBag.PageSizeList = new SelectList(new[] { 10, 25, 50 }, pageSize);

            return View(paginatedList);
        }

        // GET: Fianza/Create
        [HttpGet]
        public async Task<IActionResult> Create(int? eventoId)
        {
            var fianzaVM = new FianzaVM
            {
                FechaRegistro = DateTime.Today
            };

            if (eventoId.HasValue)
            {
                // Caso A: Venimos desde un Evento específico
                var evento = await _eventoService.GetByIdAsync(eventoId.Value);
                if (evento == null) return NotFound();

                if (evento.FianzaId.HasValue)
                {
                    TempData["Error"] = "Este evento ya tiene una fianza registrada.";
                    return RedirectToAction("Details", "Evento", new { id = eventoId });
                }

                fianzaVM.EventoId = eventoId.Value;
                fianzaVM.EventoDescripcion = $"{evento.ClienteNombreCompleto} - {evento.Tipo} ({evento.Inicio:dd/MM/yyyy})";

                // Marcamos que el evento ya está fijo
                ViewBag.EventoPreseleccionado = true;
            }
            else
            {
                // Caso B: Venimos desde el menú general
                // Cargamos el dropdown de eventos que NO tienen fianza
                ViewBag.EventosDisponibles = await _eventoService.GetEventosSinFianzaParaDropdownAsync();
                ViewBag.EventoPreseleccionado = false;
            }

            return View(fianzaVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FianzaVM fianzaVM)
        {
            if (!ModelState.IsValid)
            {
                // Recargar datos de la vista si falla la validación
                if (fianzaVM.EventoId > 0)
                {
                    var evento = await _eventoService.GetByIdAsync(fianzaVM.EventoId);
                    fianzaVM.EventoDescripcion = $"{evento.ClienteNombreCompleto} - {evento.Tipo} ({evento.Inicio:dd/MM/yyyy})";
                    ViewBag.EventoPreseleccionado = true;
                }
                else
                {
                    ViewBag.EventosDisponibles = await _eventoService.GetEventosSinFianzaParaDropdownAsync();
                    ViewBag.EventoPreseleccionado = false;
                }
                return View(fianzaVM);
            }

            var result = await _fianzaService.CreateAsync(fianzaVM);

            if (result.Success)
            {
                TempData["Ok"] = "Fianza registrada exitosamente.";
                // Volvemos al detalle del evento asociado
                return RedirectToAction("Details", "Evento", new { id = fianzaVM.EventoId });
            }

            ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault());

            // Recargar datos en caso de error de negocio
            if (fianzaVM.EventoId > 0)
            {
                var evento = await _eventoService.GetByIdAsync(fianzaVM.EventoId);
                fianzaVM.EventoDescripcion = $"{evento.ClienteNombreCompleto} - {evento.Tipo} ({evento.Inicio:dd/MM/yyyy})";
                ViewBag.EventoPreseleccionado = true;
            }
            else
            {
                ViewBag.EventosDisponibles = await _eventoService.GetEventosSinFianzaParaDropdownAsync();
                ViewBag.EventoPreseleccionado = false;
            }

            return View(fianzaVM);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var fianzaVM = await _fianzaService.GetByIdAsync(id);
            if (fianzaVM == null)
            {
                return NotFound();
            }
            return View(fianzaVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FianzaVM fianzaVM)
        {
            if (id != fianzaVM.FianzaId)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(fianzaVM);
            }

            // Aquí llamamos a la lógica de negocio que calcula si es devolución parcial/total
            var result = await _fianzaService.UpdateAsync(fianzaVM);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            string errorMsg = result.Message;
            if (string.IsNullOrEmpty(errorMsg) && result.Errors != null)
                errorMsg = result.Errors.FirstOrDefault();

            ModelState.AddModelError(string.Empty, errorMsg ?? "Error al actualizar la fianza");

            // Recargamos descripción por si acaso, para que la vista no se vea fea
            var fianzaOriginal = await _fianzaService.GetByIdAsync(id);
            if (fianzaOriginal != null)
            {
                fianzaVM.EventoDescripcion = fianzaOriginal.EventoDescripcion;
            }

            return View(fianzaVM);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _fianzaService.DeleteAsync(id);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
            }
            else
            {
                string mensajeError = !string.IsNullOrEmpty(result.Message)
                              ? result.Message
                              : result.Errors?.FirstOrDefault();

                TempData["Error"] = mensajeError;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var fianzaVM = await _fianzaService.GetByIdAsync(id);
            if (fianzaVM == null)
            {
                return NotFound();
            }
            return View(fianzaVM);
        }

    }

 
}