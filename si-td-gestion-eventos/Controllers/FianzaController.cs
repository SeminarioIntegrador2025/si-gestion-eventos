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

        // --- 1. LISTADO DE FIANZAS (Index) ---
        [HttpGet]
        public async Task<IActionResult> Index(string? q, EstadoFianza? estado, int page = 1, int pageSize = 10)
        {
            // Llamamos al servicio que nos devuelve PaginatedList<FianzaVM>
            var paginatedList = await _fianzaService.GetPaginatedAsync(q, estado, page, pageSize);

            // Pasamos los filtros a la vista para mantener el estado
            ViewBag.Search = q;
            ViewBag.Estado = estado;

            // Listas para los dropdowns de filtros
            ViewBag.EstadosList = new SelectList(Enum.GetValues(typeof(EstadoFianza)), estado);
            ViewBag.PageSizeList = new SelectList(new[] { 10, 25, 50 }, pageSize);

            return View(paginatedList);
        }

        // --- 2. ALTA DE FIANZA (Create) ---
        // GET: Fianza/Create?eventoId=5 (Desde Detalles) O Fianza/Create (Desde Index)
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
    }
}