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

        // ==========================================
        // 1. LISTADO Y FILTROS (INDEX)
        // ==========================================
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

            // Dropdown para filtros
            var eventosFilter = (await _eventoService.GetEventosParaFiltroPagosAsync()).ToList();
            var selectedValue = eventoId?.ToString();
            foreach (var item in eventosFilter)
                item.Selected = item.Value == selectedValue;

            ViewBag.EventosFilter = eventosFilter;

            // KPIs
            var pagosValidos = pagos.Where(p => p.Valido).ToList();
            ViewBag.TotalPagosPMes = pagosValidos.Count(p => p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year);
            ViewBag.TotalMontoPMes = pagosValidos.Where(p => p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year).Sum(p => p.Monto);
            ViewBag.CountTransferenciasPMes = pagosValidos.Count(p => p.Metodo == MetodoPago.Transferencia && p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year);
            ViewBag.CountEfectivoPMes = pagosValidos.Count(p => p.Metodo == MetodoPago.Efectivo && p.Fecha.Month == DateTime.Now.Month && p.Fecha.Year == DateTime.Now.Year);

            // Orden y Paginación
            pagos = pagos.OrderByDescending(p => p.Fecha).ToList();
            var total = pagos.Count;
            var pageItems = pagos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new PaginatedList<PagoVM>(pageItems, total, page, pageSize);
            ViewData["PageSize"] = pageSize;
            ViewData["Page"] = page;

            return View(model);
        }

        // ==========================================
        // 2. CREACIÓN DE PAGOS (CREATE)
        // ==========================================
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
                ViewData["EventoId"] = eventoId.Value;
                ViewData["EventoDescripcion"] = $"{eventoVM.ClienteNombreCompleto} - {eventoVM.Tipo} - {eventoVM.Inicio:dd/MM/yyyy}";
                ViewData["SaldoAnterior"] = (float)eventoVM.SaldoRestante;
                ViewData["TotalPagadoActual"] = (float)eventoVM.TotalPagado;
                ViewData["CostoTotalEvento"] = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));
                ViewData["EstadoActual"] = eventoVM.Estado.ToString();
                pagoVM.Monto = (float)(eventoVM.SaldoRestante);
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
            pagoVM.Valido = true;
            var evento = await _eventoService.GetByIdAsync(pagoVM.EventoId);

            if (evento == null)
            {
                ModelState.AddModelError(string.Empty, "El evento no existe.");
                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }

            if (!evento.PermiteAgregarPago || pagoVM.Monto > (float)evento.SaldoRestante)
            {
                ModelState.AddModelError(string.Empty, "El pago no es permitido o el monto supera el saldo.");
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
                TempData["Ok"] = result.Message;
                return RedirectToAction(nameof(Index), new { eventoId = pagoVM.EventoId });
            }

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            await PrepararDropdownEventosAsync(pagoVM.EventoId);
            return View(pagoVM);
        }

        // ==========================================
        // 3. ACTUALIZACIÓN (EDIT) - CORRECCIÓN ERROR 405
        // ==========================================

        // Acción GET: Carga el formulario de edición
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var pagoVM = await _pagoService.GetByIdAsync(id);
            if (pagoVM == null) return NotFound();

            // Solo permitimos editar pagos que no estén anulados
            if (!pagoVM.Valido)
            {
                TempData["Error"] = "No se puede editar un pago anulado.";
                return RedirectToAction("Index", new { eventoId = pagoVM.EventoId });
            }

            return View(pagoVM);
        }

        // Acción POST: Procesa los cambios
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PagoVM model)
        {
            // Solo validamos Metodo, Observaciones y ArchivoComprobante según tu requerimiento
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

        // ==========================================
        // 4. ANULACIÓN Y REPORTES
        // ==========================================
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
            if (pdfBytes == null) return NotFound("No se encontró el pago.");

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
            worksheet.Cell(1, 1).Value = "Fecha";
            worksheet.Cell(1, 2).Value = "Cliente";
            worksheet.Cell(1, 3).Value = "Monto";

            // ... (Resto de la lógica de Excel que ya tenías)

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Reporte_Pagos_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ==========================================
        // 5. WIZARD DE RESERVA (TEMPDATA)
        // ==========================================
        [HttpGet]
        public IActionResult CreateReserva()
        {
            if (TempData["PendingPaymentDetails"] is not string paymentJson || TempData["PendingEvent"] is not string eventJson)
            {
                TempData["Error"] = "Sesión expirada.";
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

        // --- HELPERS ---
        private async Task PrepararDropdownEventosAsync(int eventoId)
        {
            if (eventoId == 0) ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
        }
    }
}