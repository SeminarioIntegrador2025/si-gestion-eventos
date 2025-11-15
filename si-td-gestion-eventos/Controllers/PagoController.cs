using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Para SelectList
using Newtonsoft.Json;
using si_td_gestion_eventos.Models.Enums; // Para los KPIs
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System.Collections.Generic;
using System.Linq; // Para .Sum(), .Count(), .FirstOrDefault()
using System.Threading.Tasks; // Para async/await

namespace si_td_gestion_eventos.Controllers
{
    public class PagoController : Controller
    {
        private readonly IPagoService _pagoService;
        private readonly IEventoService _eventoService;


        public PagoController(IPagoService pagoService, IEventoService eventoService)
        {
            _pagoService = pagoService;
            _eventoService = eventoService;
        }

        // --- MÉTODO INDEX (Sin Cambios) ---
        public async Task<IActionResult> Index(int? eventoId, string q)
        {
            List<PagoVM> pagos;
            bool isFilteredByEvent = eventoId.HasValue;
            ViewData["IsFilteredByEvent"] = isFilteredByEvent;

            if (isFilteredByEvent)
            {
                pagos = await _pagoService.GetPagosByEventoIdAsync(eventoId.Value);
                ViewData["EventoId"] = eventoId.Value;
                ViewData["EventoDescripcion"] = pagos.FirstOrDefault()?.EventoDescripcion ?? "Evento no encontrado";
                ViewData["ClienteNombre"] = pagos.FirstOrDefault()?.ClienteNombre ?? "N/A";
            }
            else
            {
                pagos = await _pagoService.GetAllAsync();
                if (!string.IsNullOrEmpty(q))
                {
                    string lowerQ = q.ToLower().Trim();
                    pagos = pagos.Where(p =>
                        (p.ClienteNombre != null && p.ClienteNombre.ToLower().Contains(lowerQ)) ||
                        (p.EventoDescripcion != null && p.EventoDescripcion.ToLower().Contains(lowerQ)) ||
                        // (Aquí ya no está la 'ReferenciaComprobante' que daba error, ¡bien!)
                        p.Metodo.ToString().ToLower().Contains(lowerQ)
                    ).ToList();
                }
                ViewBag.EventosFilter = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
                ViewData["CurrentFilterQ"] = q;
            }

            //KPIs
            var today = DateTime.Today;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            var pagosDelMes = pagos.Where(p => p.Fecha.Date >= firstDayOfMonth).ToList();
            ViewBag.TotalPagosPMes = pagosDelMes.Count;
            ViewBag.TotalMontoPMes = pagosDelMes.Sum(p => p.Monto);
            ViewBag.CountTransferenciasPMes = pagosDelMes.Count(p => p.Metodo == MetodoPago.Transferencia);
            ViewBag.CountEfectivoPMes = pagosDelMes.Count(p => p.Metodo == MetodoPago.Efectivo);

            return View(pagos);
        }

        // --- MÉTODO CREATE [GET] (Con Informe Financiero) ---
        [HttpGet]
        public async Task<IActionResult> Create(int? eventoId)
        {
            var pagoVM = new PagoVM
            {
                Fecha = DateTime.Now
            };

            // 1. OBTENER LA LISTA DE EVENTOS SIEMPRE
            // (Esto es necesario para el dropdown)
            var eventosList = await _eventoService.GetEventosAdeudadosParaDropdownAsync();

            if (eventoId.HasValue)
            {
                // MODO 1: Evento pre-seleccionado
                pagoVM.EventoId = eventoId.Value;
                ViewData["EventoId"] = eventoId.Value;

                // --- Lógica del Informe Financiero (Igual que antes) ---
                var eventoVM = await _eventoService.GetByIdAsync(eventoId.Value);
                if (eventoVM != null)
                {
                    float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));

                    ViewData["SaldoAnterior"] = (float)eventoVM.SaldoRestante;
                    ViewData["TotalPagadoActual"] = (float)eventoVM.TotalPagado;
                    ViewData["CostoTotalEvento"] = costoTotal;
                    ViewData["EstadoActual"] = eventoVM.Estado.ToString();
                }
                else
                {
                    // Fallback (Datos vacíos)
                    ViewData["SaldoAnterior"] = 0f;
                    ViewData["TotalPagadoActual"] = 0f;
                    ViewData["CostoTotalEvento"] = 0f;
                    ViewData["EstadoActual"] = "N/A";
                }
            }
            else
            {
                // MODO 2: Sin evento seleccionado
                // (El informe simplemente mostrará $0)
                ViewData["SaldoAnterior"] = 0f;
                ViewData["TotalPagadoActual"] = 0f;
                ViewData["CostoTotalEvento"] = 0f;
                ViewData["EstadoActual"] = "N/A";
            }

            // 2. CREAR LA SELECTLIST
            // Le pasamos la lista Y el 'eventoId' (que puede ser null)
            // Esto se encarga de pre-seleccionar el dropdown automáticamente.
            ViewBag.Eventos = new SelectList(eventosList, "Value", "Text", eventoId);

            return View(pagoVM);
        }

        // --- MÉTODO CREATE [POST] (Con Validación de Sobrepago) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PagoVM pagoVM)
        {
            if (!ModelState.IsValid)
            {
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }

            // --- VALIDACIÓN (CU07.3.FE3 - SOBREPAGO) ---
            var evento = await _eventoService.GetByIdAsync(pagoVM.EventoId);
            if (evento != null)
            {
                var saldoRestante = (float)evento.SaldoRestante;

                if (pagoVM.Monto > (saldoRestante + 0.01)) // (Margen de 0.01 para errores de float)
                {
                    ModelState.AddModelError("Monto",
                        $"El monto no puede superar el saldo pendiente de ${saldoRestante:N2}");

                    // Recargamos los datos del informe y el dropdown
                    await PrepararDropdownEventosAsync(pagoVM.EventoId);
                    ViewData["EventoDescripcion"] = $"{evento.ClienteNombreCompleto} - {evento.Tipo} - {evento.Inicio:dd/MM/yyyy}";
                    ViewData["SaldoAnterior"] = saldoRestante;
                    ViewData["TotalPagadoActual"] = (float)evento.TotalPagado;
                    ViewData["CostoTotalEvento"] = (float)(evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0));
                    ViewData["EstadoActual"] = evento.Estado.ToString();

                    return View(pagoVM);
                }
            }
            // --- FIN DE LA VALIDACIÓN ---

            var result = await _pagoService.CreateAsync(pagoVM);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Index), new { eventoId = pagoVM.EventoId });
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }
        }

        // --- MÉTODO DescargarRecibo (Sin Cambios) ---
        [HttpGet]
        public async Task<IActionResult> DescargarRecibo(int pagoId)
        {
            var pdfBytes = await _pagoService.GenerarReciboPdfAsync(pagoId);
            if (pdfBytes == null)
            {
                return NotFound("No se encontró el pago.");
            }
            string nombreArchivo = $"Recibo-Pago-{pagoId}-{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", nombreArchivo);
        }

        // --- MÉTODO PrepararDropdown (Sin Cambios) ---
        private async Task PrepararDropdownEventosAsync(int eventoId)
        {
            if (eventoId == 0)
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }
        }


        // --- MÉTODO CreateReserva [GET] (Con Sugerencias a JS) ---
        [HttpGet]
        public IActionResult CreateReserva()
        {
            if (TempData["PendingPaymentDetails"] is not string paymentJson)
            {
                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }
            var pagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);

            // --- PASAR DATOS A JS ---
            if (TempData["PendingEvent"] is not string eventJson)
            {
                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }
            var eventoVm = JsonConvert.DeserializeObject<EventoVM>(eventJson);

            float costoTotal = (float)(eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0));

            ViewData["CostoTotalEvento"] = costoTotal;
            ViewData["ObsReserva"] = "Por Reserva";
            ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)";
            // --- FIN ---

            TempData.Keep("PendingEvent");
            TempData.Keep("PendingPaymentDetails");

            return View(pagoVm);
        }

        // --- MÉTODO CreateReserva [POST] (Con Repoblado de ViewData) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReserva(PagoReservaVM pagoVm)
        {
            if (TempData["PendingEvent"] is not string eventJson)
            {
                TempData["Error"] = "La sesión ha expirado. Por favor, intente crear el evento de nuevo.";
                return RedirectToAction("Create", "Evento");
            }
            if (TempData["PendingPaymentDetails"] is not string paymentJson)
            {
                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }

            var eventoVm = JsonConvert.DeserializeObject<EventoVM>(eventJson);
            var originalPagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);
            float costoTotal = (float)(eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0));

            if (!ModelState.IsValid)
            {
                pagoVm.Monto = originalPagoVm.Monto;
                pagoVm.Fecha = originalPagoVm.Fecha;
                pagoVm.Observaciones = originalPagoVm.Observaciones;

                TempData.Keep("PendingEvent");
                TempData.Keep("PendingPaymentDetails");

                // --- AÑADIDO: Repoblar ViewData para el JS ---
                ViewData["CostoTotalEvento"] = costoTotal;
                ViewData["ObsReserva"] = "Por Reserva";
                ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)";
                // ---

                return View(pagoVm);
            }

            var result = await _eventoService.CreateEventWithPaymentAsync(eventoVm, pagoVm);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction("Index", "Evento");
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                TempData.Keep("PendingEvent");
                TempData.Keep("PendingPaymentDetails");

                pagoVm.Monto = originalPagoVm.Monto;
                pagoVm.Fecha = originalPagoVm.Fecha;
                pagoVm.Observaciones = originalPagoVm.Observaciones;

                // --- AÑADIDO: Repoblar ViewData para el JS ---
                ViewData["CostoTotalEvento"] = costoTotal;
                ViewData["ObsReserva"] = "Por Reserva";
                ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)";
                // ---

                return View(pagoVm);
            }
        }

        // --- MÉTODO CancelCreate (Sin Cambios) ---
        [HttpGet]
        public IActionResult CancelCreate()
        {
            TempData.Remove("PendingEvent");
            TempData.Remove("PendingPaymentDetails");

            TempData["Info"] = "Creación de evento cancelada.";
            return RedirectToAction("Index", "Evento");
        }
    }
}