using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var fianzaVM = new FianzaVM
            {
                FechaRegistro = DateTime.Today
            };

            if (eventoId.HasValue)
            {
                // Caso A: Venimos desde un Evento específico
                var evento = await _eventoService.GetByIdAsync(eventoId.Value);
                if (evento == null) return NotFound();

                // Validar si ya tiene fianza
                if (evento.FianzaId.HasValue)
                {
                    TempData["Error"] = "Este evento ya tiene una fianza registrada.";
                    return RedirectToAction("Details", "Evento", new { id = eventoId });
                }

                fianzaVM.EventoId = eventoId.Value;
                // Usamos el helper para cargar la info de la vista
                await CargarDatosVistaCreate(fianzaVM);
            }
            else
            {
                // Caso B: Venimos desde el menú general
                await CargarDatosVistaCreate(fianzaVM);
            }

            return View(fianzaVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FianzaVM fianzaVM)
        {
            // 1. Validación del Modelo (FluentValidation se ejecuta aquí automáticamente)
            if (!ModelState.IsValid)
            {
                await CargarDatosVistaCreate(fianzaVM); // Recarga datos necesarios para la vista
                return View(fianzaVM);
            }

            // 2. Intentar crear la fianza
            var result = await _fianzaService.CreateAsync(fianzaVM);

            if (result.Success)
            {
                TempData["Ok"] = "Fianza registrada exitosamente.";
                // Volvemos al detalle del evento asociado
                return RedirectToAction("Details", "Evento", new { id = fianzaVM.EventoId });
            }

            // 3. Si falla la lógica de negocio, mostramos el error
            ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault());

            await CargarDatosVistaCreate(fianzaVM); // Recarga datos necesarios para la vista
            return View(fianzaVM);
        }

        // --- EDITAR (EDIT) ---
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
                return BadRequest();

            if (!ModelState.IsValid)
            {
                // Recargar datos visuales (Descripción del evento) para que no se pierdan
                var fianzaOriginal = await _fianzaService.GetByIdAsync(id);
                if (fianzaOriginal != null)
                    fianzaVM.EventoDescripcion = fianzaOriginal.EventoDescripcion;

                return View(fianzaVM);
            }

            var result = await _fianzaService.UpdateAsync(fianzaVM);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            // Mostrar errores de negocio
            if (!string.IsNullOrEmpty(result.Message))
                ModelState.AddModelError(string.Empty, result.Message);

            foreach (var error in result.Errors ?? new List<string>())
                ModelState.AddModelError(string.Empty, error);

            // Recargar datos visuales
            var fianza = await _fianzaService.GetByIdAsync(id);
            if (fianza != null)
                fianzaVM.EventoDescripcion = fianza.EventoDescripcion;

            return View(fianzaVM);
        }

        // --- ELIMINAR (DELETE) ---
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

        // --- DETALLES (DETAILS) ---
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

        // --- MÉTODOS PRIVADOS DE AYUDA (HELPERS) ---

        /// <summary>
        /// Carga los datos necesarios para la vista Create (Dropdowns o Descripción del Evento)
        /// dependiendo de si ya se seleccionó un evento o no.
        /// </summary>
        private async Task CargarDatosVistaCreate(FianzaVM fianzaVM)
        {
            if (fianzaVM.EventoId > 0)
            {
                // Si ya hay un evento seleccionado (ya sea por GET o POST), cargamos su descripción
                var evento = await _eventoService.GetByIdAsync(fianzaVM.EventoId);
                if (evento != null)
                {
                    fianzaVM.EventoDescripcion = $"{evento.ClienteNombreCompleto} - {evento.Tipo} ({evento.Inicio:dd/MM/yyyy})";
                    ViewBag.EventoPreseleccionado = true;
                }
            }
            else
            {
                // Si no hay evento, cargamos la lista desplegable de eventos disponibles (sin fianza)
                ViewBag.EventosDisponibles = await _eventoService.GetEventosSinFianzaParaDropdownAsync();
                ViewBag.EventoPreseleccionado = false;
            }
        }
    }
}