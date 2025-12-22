using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using ClosedXML.Excel;
using System.IO;

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

        #region 1. LISTADO Y FILTROS (INDEX)

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
                var primerPago = pagos.FirstOrDefault();
                ViewData["EventoDescripcion"] = primerPago?.EventoDescripcion ?? "Evento sin pagos registrados";
                ViewData["ClienteNombre"] = primerPago?.ClienteNombre ?? "N/A";
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

            // Dropdown para filtros de la vista
            ViewBag.EventosFilter = (await _eventoService.GetEventosParaFiltroPagosAsync()).ToList();

            // KPIs - Estadísticas del mes actual
            var pagosValidosMes = pagos.Where(p => p.Valido && p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year).ToList();
            ViewBag.TotalPagosPMes = pagosValidosMes.Count;
            ViewBag.TotalMontoPMes = pagosValidosMes.Sum(p => p.Monto);
            ViewBag.CountTransferenciasPMes = pagosValidosMes.Count(p => p.Metodo == MetodoPago.Transferencia);
            ViewBag.CountEfectivoPMes = pagosValidosMes.Count(p => p.Metodo == MetodoPago.Efectivo);

            // Orden y Paginación
            pagos = pagos.OrderByDescending(p => p.Fecha).ToList();
            var total = pagos.Count;
            var pageItems = pagos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new PaginatedList<PagoVM>(pageItems, total, page, pageSize);
            return View(model);
        }

        #endregion

        #region 2. CREACIÓN DE PAGOS (CREATE)

        [HttpGet]
        public async Task<IActionResult> Create(int? eventoId)
        {
            var pagoVM = new PagoVM { Fecha = DateTime.Now, Valido = true };

            if (eventoId.HasValue)
            {
                var eventoVM = await _eventoService.GetByIdAsync(eventoId.Value);
                if (eventoVM == null) return NotFound();

                if (eventoVM.SaldoRestante <= 0 && eventoVM.Estado != EventoEstado.Cancelado)
                {
                    TempData["Info"] = "Este evento ya se encuentra totalmente saldado.";
                    return RedirectToAction("Index", new { eventoId = eventoId.Value });
                }

                pagoVM.EventoId = eventoId.Value;
                pagoVM.Monto = (float)eventoVM.SaldoRestante;

                await CargarDatosContextoPagoAsync(eventoId.Value);
            }
            else
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }

            return View(pagoVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PagoVM pagoVM)
        {
            // Dejamos que el PagoValidator (FluentValidation) valide el ModelState automáticamente
            if (!ModelState.IsValid)
            {
                await CargarDatosContextoPagoAsync(pagoVM.EventoId);
                return View(pagoVM);
            }

            var result = await _pagoService.CreateAsync(pagoVM);
            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction(nameof(Index), new { eventoId = pagoVM.EventoId });
            }

            // Si el servicio detecta un error de negocio adicional
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            await CargarDatosContextoPagoAsync(pagoVM.EventoId);
            return View(pagoVM);
        }

        #endregion

        #region 3. GESTIÓN Y REPORTES

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var pagoVM = await _pagoService.GetByIdAsync(id);
            if (pagoVM == null) return NotFound();

            if (!pagoVM.Valido)
            {
                TempData["Error"] = "No se puede editar un pago que ha sido anulado.";
                return RedirectToAction("Index", new { eventoId = pagoVM.EventoId });
            }

            return View(pagoVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PagoVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _pagoService.UpdateAsync(model);
            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction("Details", "Evento", new { id = model.EventoId });
            }

            TempData["Error"] = result.Message;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id)
        {
            var pagoExistente = await _pagoService.GetByIdAsync(id);
            if (pagoExistente == null) return NotFound();

            var result = await _pagoService.AnularPagoAsync(id);
            if (result.Success) TempData["Ok"] = result.Message;
            else TempData["Error"] = result.Message;

            return RedirectToAction("Index", new { eventoId = pagoExistente.EventoId });
        }

        [HttpGet]
        public async Task<IActionResult> DescargarRecibo(int pagoId)
        {
            var pdfBytes = await _pagoService.GenerarReciboPdfAsync(pagoId);
            if (pdfBytes == null) return NotFound("No se encontró el registro del pago.");

            return File(pdfBytes, "application/pdf", $"Recibo-Pago-{pagoId}-{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ExportarExcel(int? eventoId, string q)
        {
            List<PagoVM> pagos = eventoId.HasValue
                ? await _pagoService.GetPagosByEventoIdAsync(eventoId.Value)
                : await _pagoService.GetAllAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Pagos");

            // Lógica de exportación...

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Reporte_Pagos_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        #endregion

        #region 4. WIZARD RESERVA (TEMPDATA)

        [HttpGet]
        public IActionResult CreateReserva()
        {
            if (TempData["PendingPaymentDetails"] is not string paymentJson || TempData["PendingEvent"] is not string eventJson)
            {
                TempData["Error"] = "La sesión de reserva ha expirado.";
                return RedirectToAction("Create", "Evento");
            }

            var pagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);
            var eventoVm = JsonConvert.DeserializeObject<EventoVM>(eventJson);

            ViewData["CostoTotalEvento"] = (float)(eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0));
            TempData.Keep();
            return View(pagoVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReserva(PagoReservaVM pagoVm)
        {
            if (TempData["PendingEvent"] is not string eventJson) return RedirectToAction("Create", "Evento");

            var eventoVm = JsonConvert.DeserializeObject<EventoVM>(eventJson);
            var result = await _eventoService.CreateEventWithPaymentAsync(eventoVm, pagoVm);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction("Index", "Evento");
            }

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            TempData.Keep();
            return View(pagoVm);
        }

        #endregion

        #region HELPERS PRIVADOS

        /// <summary>
        /// Esta es la solución al BUG: Centraliza la carga de datos financieros para la vista.
        /// Se llama en el GET inicial y en el POST cuando hay errores de validación.
        /// </summary>
        private async Task CargarDatosContextoPagoAsync(int eventoId)
        {
            if (eventoId == 0)
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
                return;
            }

            var evento = await _eventoService.GetByIdAsync(eventoId);
            if (evento != null)
            {
                ViewData["EventoId"] = evento.EventoId;
                ViewData["EventoDescripcion"] = $"{evento.ClienteNombreCompleto} - {evento.Tipo} - {evento.Inicio:dd/MM/yyyy}";
                ViewData["SaldoAnterior"] = (float)evento.SaldoRestante;
                ViewData["TotalPagadoActual"] = (float)evento.TotalPagado;
                ViewData["CostoTotalEvento"] = (float)(evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0));
                ViewData["EstadoActual"] = evento.Estado.ToString();
            }
        }

        #endregion
    }
}