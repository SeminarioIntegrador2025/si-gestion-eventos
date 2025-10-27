// En: Controllers/PagoController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Para SelectList
using si_td_gestion_eventos.Models.Enums;  // Para los KPIs
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System.Collections.Generic; // Para List<T>
using System.Linq; // Para .Sum(), .Count(), .FirstOrDefault()
using System.Threading.Tasks; // Para async/await

namespace si_td_gestion_eventos.Controllers
{
    public class PagoController : Controller
    {
        private readonly IPagoService _pagoService;
        private readonly IEventoService _eventoService;

        // Inyectamos ambos servicios
        public PagoController(IPagoService pagoService, IEventoService eventoService)
        {
            _pagoService = pagoService;
            _eventoService = eventoService;
        }

        // --- MÉTODO INDEX (MODIFICADO) ---
        // Maneja /Pago (global) Y /Pago?eventoId=5 (filtrado)
        public async Task<IActionResult> Index(int? eventoId, string q)
        {
            List<PagoVM> pagos;
            bool isFilteredByEvent = eventoId.HasValue;
            ViewData["IsFilteredByEvent"] = isFilteredByEvent;

            if (isFilteredByEvent)
            {
                // --- MODO 1: Filtrado por Evento ---
                pagos = await _pagoService.GetPagosByEventoIdAsync(eventoId.Value);

                ViewData["EventoId"] = eventoId.Value;
                ViewData["EventoDescripcion"] = pagos.FirstOrDefault()?.EventoDescripcion ?? "Evento no encontrado";
                ViewData["ClienteNombre"] = pagos.FirstOrDefault()?.ClienteNombre ?? "N/A";
            }
            else
            {
                // --- MODO 2: Global (Estilo Clientes) ---
                pagos = await _pagoService.GetAllAsync();

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(q))
                {
                    string lowerQ = q.ToLower().Trim();
                    pagos = pagos.Where(p =>
                        (p.ClienteNombre != null && p.ClienteNombre.ToLower().Contains(lowerQ)) ||
                        (p.EventoDescripcion != null && p.EventoDescripcion.ToLower().Contains(lowerQ)) ||
                        (p.ReferenciaComprobante != null && p.ReferenciaComprobante.ToLower().Contains(lowerQ)) ||
                        p.Metodo.ToString().ToLower().Contains(lowerQ)
                    ).ToList();
                }

                // Cargar dropdown de Eventos para el FILTRO
                ViewBag.EventosFilter = await _eventoService.GetEventosActivosParaDropdownAsync();
                ViewData["CurrentFilterQ"] = q;
            }

            // --- KPIs (Se calculan para ambos modos) ---
            ViewBag.TotalPagos = pagos.Count;
            ViewBag.TotalMonto = pagos.Sum(p => p.Monto);
            ViewBag.CountTransferencias = pagos.Count(p => p.Metodo == MetodoPago.Transferencia);
            ViewBag.CountEfectivo = pagos.Count(p => p.Metodo == MetodoPago.Efectivo);

            return View(pagos);
        }

        // --- MÉTODO CREATE (GET - MODIFICADO) ---
        // Maneja /Pago/Create Y /Pago/Create?eventoId=5
        public async Task<IActionResult> Create(int? eventoId)
        {
            var pagoVM = new PagoVM
            {
                // Usamos FechaPago por consistencia (tu snippet original usaba 'Fecha')
                Fecha = DateTime.Now
            };

            if (eventoId.HasValue)
            {
                // MODO 1: Se crea desde un evento específico
                pagoVM.EventoId = eventoId.Value;
                // Pasamos el ID para el botón "Cancelar"
                ViewData["EventoId"] = eventoId.Value;
            }
            else
            {
                // MODO 2: Se crea desde la página global
                // (Este es tu "getEventosParaDropdown")
                ViewBag.Eventos = await _eventoService.GetEventosActivosParaDropdownAsync();
            }

            return View(pagoVM);
        }

        // --- MÉTODO CREATE (POST - MODIFICADO) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PagoVM pagoVM)
        {
            // 1. Validación del Modelo (Tu línea 107)
            if (!ModelState.IsValid)
            {
                // Si la validación falla, recargamos el dropdown
                await PrepararDropdownEventosAsync(pagoVM.EventoId); // (Llama al helper)
                return View(pagoVM);
            }

            // 2. Llama al servicio (Tu línea 117)
            var result = await _pagoService.CreateAsync(pagoVM);

            // 3. Verifica el resultado del servicio
            // Asumiendo que tu ServiceResult tiene la propiedad 'Success' (tal como en tu foto)
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Index), new { eventoId = pagoVM.EventoId });
            }
            else
            {
                // --- ESTE ES EL CÓDIGO QUE TE FALTA (Arregla CS0161) ---
                // Falla del servicio. Agrega los errores.
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                // Recargamos el dropdown, igual que en el fallo de ModelState
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
                // --------------------------------------------------------
            }
        }

        private async Task PrepararDropdownEventosAsync(int eventoId)
        {
            // Solo recargamos el dropdown si el EventoId es 0 (formulario global)
            if (eventoId == 0)
            {
                ViewBag.Eventos = await _eventoService.GetEventosActivosParaDropdownAsync();
            }
        }
    }

}