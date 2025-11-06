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
        // /Pago (global) Y /Pago?eventoId=5 (filtrado)
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

        // --- MÉTODO CREATE [GET] (MODIFICADO) ---
        // Maneja /Pago/Create Y /Pago/Create?eventoId=5
        // Añade la lógica para pasar los datos del Informe Financiero a la vista
        [HttpGet]
        public async Task<IActionResult> Create(int? eventoId)
        {
            var pagoVM = new PagoVM
            {
                Fecha = DateTime.Now
            };

            if (eventoId.HasValue)
            {
                pagoVM.EventoId = eventoId.Value;
                ViewData["EventoId"] = eventoId.Value;

                // --- INICIO DE LA MODIFICACIÓN (INFORME FINANCIERO) ---
                var eventoVM = await _eventoService.GetByIdAsync(eventoId.Value);
                if (eventoVM != null)
                {
                    // Calculamos el costo total real (con aire)
                    float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));

                    // Creamos la descripción del evento para el input deshabilitado
                    string eventoDescripcion = $"{eventoVM.ClienteNombreCompleto} - {eventoVM.Tipo} - {eventoVM.Inicio:dd/MM/yyyy}";
                    ViewData["EventoDescripcion"] = eventoDescripcion;

                    // Pasamos los datos al ViewData para el informe y el JS
                    ViewData["SaldoAnterior"] = (float)eventoVM.SaldoRestante;
                    ViewData["TotalPagadoActual"] = (float)eventoVM.TotalPagado;
                    ViewData["CostoTotalEvento"] = costoTotal;
                    ViewData["EstadoActual"] = eventoVM.Estado.ToString();
                }
                else
                {
                    // Fallback por si no se encuentra
                    ViewData["EventoDescripcion"] = "Evento no encontrado";
                    ViewData["SaldoAnterior"] = 0f;
                    ViewData["TotalPagadoActual"] = 0f;
                    ViewData["CostoTotalEvento"] = 0f;
                    ViewData["EstadoActual"] = "N/A";
                }
                // --- FIN DE LA MODIFICACIÓN ---
            }
            else
            {
                // Modo global, solo cargamos el dropdown
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }

            return View(pagoVM);
        }

        // --- MÉTODO CREATE [POST] (Sin Cambios) ---
        // Tu lógica aquí está perfecta.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PagoVM pagoVM)
        {
            if (!ModelState.IsValid)
            {
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }

            // (Aquí es donde deberías añadir la validación de FE3 - Monto Excedido)
            // var evento = await _eventoService.GetByIdAsync(pagoVM.EventoId);
            // var saldoRestante = (float)evento.SaldoRestante;
            // if (pagoVM.Monto > saldoRestante) { ... }

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


        // --- MÉTODO CreateReserva [GET] (MODIFICADO) ---
        // Añade la lógica para pasar el "CostoTotal" al JavaScript de la vista
        [HttpGet]
        public IActionResult CreateReserva()
        {
            if (TempData["PendingPaymentDetails"] is not string paymentJson)
            {
                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }
            var pagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);

            // --- INICIO DE LA MODIFICACIÓN (PASAR COSTO TOTAL) ---

            // 1. Necesitamos también los datos del evento (del Paso 1)
            if (TempData["PendingEvent"] is not string eventJson)
            {
                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }
            var eventoVm = JsonConvert.DeserializeObject<EventoVM>(eventJson);

            // 2. Calculamos el Costo Total REAL
            float costoTotal = (float)(eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0));

            // 3. Pasamos los datos que el JavaScript necesitará
            ViewData["CostoTotalEvento"] = costoTotal;
            ViewData["ObsReserva"] = "Por Reserva"; // Texto para pago parcial
            ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)"; // Texto para pago total

            // --- FIN DE LA MODIFICACIÓN ---

            TempData.Keep("PendingEvent");
            TempData.Keep("PendingPaymentDetails");

            return View(pagoVm);
        }

        // --- MÉTODO CreateReserva [POST] (Sin Cambios) ---
        // Tu lógica aquí está perfecta.
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

            if (!ModelState.IsValid)
            {
                pagoVm.Monto = originalPagoVm.Monto;
                pagoVm.Fecha = originalPagoVm.Fecha;
                pagoVm.Observaciones = originalPagoVm.Observaciones;

                TempData.Keep("PendingEvent");
                TempData.Keep("PendingPaymentDetails");

                // --- (Necesitás volver a pasar el costo total si la validación falla) ---
                ViewData["CostoTotalEvento"] = eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0);
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

                // --- (Necesitás volver a pasar el costo total si la transacción falla) ---
                ViewData["CostoTotalEvento"] = eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0);
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