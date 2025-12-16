using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


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

        // MÉTODO INDEX

        public async Task<IActionResult> Index(int? eventoId, string q, int page = 1, int pageSize = 10)
        {
            var allowed = new[] { 10, 25, 50 };
            if (!allowed.Contains(pageSize)) pageSize = 10;
            if (page < 1) page = 1;

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

                if (!string.IsNullOrWhiteSpace(q))
                {
                    string lowerQ = q.ToLower().Trim();
                    pagos = pagos.Where(p =>
                        (p.ClienteNombre != null && p.ClienteNombre.ToLower().Contains(lowerQ)) ||
                        (p.EventoDescripcion != null && p.EventoDescripcion.ToLower().Contains(lowerQ)) ||
                        p.Metodo.ToString().ToLower().Contains(lowerQ)
                    ).ToList();
                }

                ViewData["CurrentFilterQ"] = q;
            }

            // ✅ Dropdown SIEMPRE (estés filtrado o no)
            var eventosFilter = (await _eventoService.GetEventosParaFiltroPagosAsync()).ToList();
            var selectedValue = eventoId?.ToString();
            foreach (var item in eventosFilter)
                item.Selected = item.Value == selectedValue;

            ViewBag.EventosFilter = eventosFilter;

            // KPIs (sobre la lista completa filtrada, NO paginada)
            var pagosValidos = pagos.Where(p => p.Valido).ToList();
            ViewBag.TotalPagosPMes = pagosValidos.Count(p => p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year);
            ViewBag.TotalMontoPMes = pagosValidos.Where(p => p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year).Sum(p => p.Monto);
            ViewBag.CountTransferenciasPMes = pagosValidos.Count(p => p.Metodo == MetodoPago.Transferencia && p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year);
            ViewBag.CountEfectivoPMes = pagosValidos.Count(p => p.Metodo == MetodoPago.Efectivo && p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year);

            // Orden + paginación
            pagos = pagos.OrderByDescending(p => p.Fecha).ToList();
            var total = pagos.Count;
            var pageItems = pagos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new PaginatedList<PagoVM>(pageItems, total, page, pageSize);
            ViewData["PageSize"] = pageSize;
            ViewData["Page"] = page;

            return View(model);
        }



        // --- NUEVO MÉTODO: ANULAR PAGO ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id)
        {
            // 1. Obtenemos el pago primero para saber a qué evento pertenece (para el redirect)
            var pagoExistente = await _pagoService.GetByIdAsync(id);

            if (pagoExistente == null)
            {
                return NotFound();
            }

            // 2. Llamamos al servicio
            var result = await _pagoService.AnularPagoAsync(id);

            // 3. Feedback al usuario
            if (result.Success)
            {
                // Usamos "SuccessMessage" para mantener consistencia con tu método Create
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                // Unimos los errores si hay varios, o usamos el mensaje general
                string msgError = result.Errors != null && result.Errors.Any()
                                  ? string.Join(", ", result.Errors)
                                  : result.Message;
                TempData["Error"] = msgError; // O "ErrorMessage", asegúrate de usar la misma key en tu _Layout
            }

            // 4. Redirección inteligente
            // Si estamos filtrando por evento, volvemos al detalle de ese evento.
            // Si no, volvemos al Index general.
            // Ajusta "Details" y "Evento" según como se llame tu acción de ver detalles del evento.
            return RedirectToAction("Index", new { eventoId = pagoExistente.EventoId });
        }

        // MÉTODO CREATE GET
        [HttpGet]
        public async Task<IActionResult> Create(int? eventoId)
        {
            var pagoVM = new PagoVM
            {
                Fecha = DateTime.Now,
                Valido = true // Aseguramos que nazca vivo (aunque el default del VM ya lo hace)
            };

            if (eventoId.HasValue)
            {
                pagoVM.EventoId = eventoId.Value;
                ViewData["EventoId"] = eventoId.Value;

                var eventoVM = await _eventoService.GetByIdAsync(eventoId.Value);
                if (eventoVM != null)
                {
                    float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));
                    string eventoDescripcion = $"{eventoVM.ClienteNombreCompleto} - {eventoVM.Tipo} - {eventoVM.Inicio:dd/MM/yyyy}";
                    ViewData["EventoDescripcion"] = eventoDescripcion;

                    ViewData["SaldoAnterior"] = (float)eventoVM.SaldoRestante;
                    ViewData["TotalPagadoActual"] = (float)eventoVM.TotalPagado;
                    ViewData["CostoTotalEvento"] = costoTotal;
                    ViewData["EstadoActual"] = eventoVM.Estado.ToString();
                }
                else
                {
                    ViewData["EventoDescripcion"] = "Evento no encontrado";
                    ViewData["SaldoAnterior"] = 0f;
                    ViewData["TotalPagadoActual"] = 0f;
                    ViewData["CostoTotalEvento"] = 0f;
                    ViewData["EstadoActual"] = "N/A";
                }
            }
            else
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }

            return View(pagoVM);
        }

        // MÉTODO CREATE [POST] 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PagoVM pagoVM)
        {
            // Forzamos true al crear
            pagoVM.Valido = true;

            // VALIDACIÓN ADICIONAL: Verificar que el evento permita agregar pago
            var evento = await _eventoService.GetByIdAsync(pagoVM.EventoId);
            if (evento == null)
            {
                ModelState.AddModelError(string.Empty, "El evento no existe.");
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }

            if (!evento.PermiteAgregarPago)
            {
                string razon = evento.Estado == EventoEstado.Cancelado ? "está cancelado" :
                               evento.Estado == EventoEstado.Realizado ? "ya fue realizado" :
                               evento.SaldoRestante <= 0 ? "está completamente pagado" :
                               evento.EsFechaIndefinida ? "está reprogramado sin fecha definida" :
                               "no permite agregar pagos";

                ModelState.AddModelError(string.Empty, $"No se puede agregar un pago porque el evento {razon}.");
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }

            if (!ModelState.IsValid)
            {
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }

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

        // MÉTODO DescargarRecibo
        [HttpGet]
        public async Task<IActionResult> DescargarRecibo(int pagoId)
        {
            // Opcional: Podrías validar aquí si el pago es válido antes de dejar descargarlo.
            // Pero generalmente se permite descargar recibos anulados como evidencia.

            var pdfBytes = await _pagoService.GenerarReciboPdfAsync(pagoId);
            if (pdfBytes == null)
            {
                return NotFound("No se encontró el pago.");
            }
            string nombreArchivo = $"Recibo-Pago-{pagoId}-{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", nombreArchivo);
        }

        private async Task PrepararDropdownEventosAsync(int eventoId)
        {
            if (eventoId == 0)
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }
        }

        // MÉTODO CreateReserva [GET] 
        [HttpGet]
        public IActionResult CreateReserva()
        {
            if (TempData["PendingPaymentDetails"] is not string paymentJson)
            {
                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }
            var pagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);

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

            TempData.Keep("PendingEvent");
            TempData.Keep("PendingPaymentDetails");

            return View(pagoVm);
        }

        // MÉTODO CreateReserva [POST]
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

                ViewData["CostoTotalEvento"] = eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0);
                ViewData["ObsReserva"] = "Por Reserva";
                ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)"; // Falta punto y coma en tu código original, corregido.

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

                ViewData["CostoTotalEvento"] = eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0);
                ViewData["ObsReserva"] = "Por Reserva";
                ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)";

                return View(pagoVm);
            }
        }

        // MÉTODO CancelCreate
        public IActionResult CancelCreate()
        {
            TempData.Remove("PendingEvent");
            TempData.Remove("PendingPaymentDetails");

            TempData["Info"] = "Creación de evento cancelada.";
            return RedirectToAction("Index", "Evento");
        }
    }
}