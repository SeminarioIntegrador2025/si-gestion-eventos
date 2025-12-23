using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;

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

        // --- LISTADO (INDEX) ---
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

        // --- CREAR (CREATE) ---
        [HttpGet]
        public async Task<IActionResult> Create(int? eventoId)
        {
            var fianzaVM = new FianzaVM { FechaRegistro = DateTime.Today };

            if (eventoId.HasValue)
            {
                var evento = await _eventoService.GetByIdAsync(eventoId.Value);
                if (evento == null) return NotFound();

                if (evento.FianzaId.HasValue)
                {
                    TempData["Error"] = "Este evento ya tiene una fianza registrada.";
                    return RedirectToAction("Details", "Evento", new { id = eventoId });
                }

                fianzaVM.EventoId = eventoId.Value;
            }

            await CargarDatosVistaCreate(fianzaVM);
            return View(fianzaVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FianzaVM fianzaVM)
        {
            if (!ModelState.IsValid)
            {
                await CargarDatosVistaCreate(fianzaVM);
                return View(fianzaVM);
            }

            var result = await _fianzaService.CreateAsync(fianzaVM);

            if (result.Success)
            {
                TempData["Ok"] = "Fianza registrada exitosamente.";
                return RedirectToAction("Details", "Evento", new { id = fianzaVM.EventoId });
            }

            ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault() ?? "Error al crear fianza.");
            await CargarDatosVistaCreate(fianzaVM);
            return View(fianzaVM);
        }

        // --- EDITAR (EDIT) ---
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var fianzaVM = await _fianzaService.GetByIdAsync(id);
            if (fianzaVM == null) return NotFound();
            return View(fianzaVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FianzaVM fianzaVM)
        {
            if (id != fianzaVM.FianzaId) return BadRequest();

            // 1. Obtener datos originales de la DB (Punto de Verdad)
            var fianzaOriginal = await _fianzaService.GetByIdAsync(id);
            if (fianzaOriginal == null) return NotFound();

            // 2. Validación manual de monto (Capa de protección inmediata)
            if (fianzaVM.MontoDevuelto > fianzaOriginal.Monto)
            {
                ModelState.AddModelError("MontoDevuelto", $"El monto a devolver no puede ser mayor al original ({fianzaOriginal.Monto:C2}).");
            }

            // 3. Manejo de fallo en validación
            if (!ModelState.IsValid)
            {
                // RESTAURACIÓN DE ESTADO (Solución al bug visual)
                // Devolvemos el VM pero forzamos que el Estado y el Monto vuelvan a ser los reales
                fianzaVM.Estado = fianzaOriginal.Estado;
                fianzaVM.Monto = fianzaOriginal.Monto;
                fianzaVM.EventoDescripcion = fianzaOriginal.EventoDescripcion;

                return View(fianzaVM);
            }

            // 4. Intento de actualización en el servicio
            var result = await _fianzaService.UpdateAsync(fianzaVM);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            // 5. Si el servicio deniega (ej: por lógica de negocio profunda)
            ModelState.AddModelError(string.Empty, result.Message ?? "Error al actualizar.");

            // Re-restauramos datos visuales para que la vista no se rompa
            fianzaVM.Estado = fianzaOriginal.Estado;
            fianzaVM.Monto = fianzaOriginal.Monto;
            fianzaVM.EventoDescripcion = fianzaOriginal.EventoDescripcion;

            return View(fianzaVM);
        }

        // --- ELIMINAR Y DETALLES (Mantener igual) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _fianzaService.DeleteAsync(id);
            if (result.Success) TempData["Ok"] = result.Message;
            else TempData["Error"] = result.Message ?? result.Errors?.FirstOrDefault();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var fianzaVM = await _fianzaService.GetByIdAsync(id);
            return fianzaVM == null ? NotFound() : View(fianzaVM);
        }

        private async Task CargarDatosVistaCreate(FianzaVM fianzaVM)
        {
            if (fianzaVM.EventoId > 0)
            {
                var evento = await _eventoService.GetByIdAsync(fianzaVM.EventoId);
                if (evento != null)
                {
                    fianzaVM.EventoDescripcion = $"{evento.ClienteNombreCompleto} - {evento.Tipo} ({evento.Inicio:dd/MM/yyyy})";
                    ViewBag.EventoPreseleccionado = true;
                    return;
                }
            }
            ViewBag.EventosDisponibles = await _eventoService.GetEventosSinFianzaParaDropdownAsync();
            ViewBag.EventoPreseleccionado = false;
        }
    }
}