using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ClosedXML.Excel; // Asegúrate de tener este using si vas a usar el Excel aquí también

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

        // --- LISTADO (INDEX) ---
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
            ViewBag.TotalPagosPMes = pagosValidos.Count(p => p.Fecha.Month == System.DateTime.Now.Month && p.Fecha.Year == System.DateTime.Now.Year);
            ViewBag.TotalMontoPMes = pagosValidos.Where(p => p.Fecha.Month == System.DateTime.Now.Month && p.Fecha.Year == System.DateTime.Now.Year).Sum(p => p.Monto);
            ViewBag.CountTransferenciasPMes = pagosValidos.Count(p => p.Metodo == MetodoPago.Transferencia && p.Fecha.Month == System.DateTime.Now.Month && p.Fecha.Year == System.DateTime.Now.Year);
            ViewBag.CountEfectivoPMes = pagosValidos.Count(p => p.Metodo == MetodoPago.Efectivo && p.Fecha.Month == System.DateTime.Now.Month && p.Fecha.Year == System.DateTime.Now.Year);

            // Orden y Paginación
            pagos = pagos.OrderByDescending(p => p.Fecha).ToList();
            var total = pagos.Count;
            var pageItems = pagos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new PaginatedList<PagoVM>(pageItems, total, page, pageSize);
            ViewData["PageSize"] = pageSize;
            ViewData["Page"] = page;

            return View(model);
        }

        // --- CREAR (CREATE) GET ---
        [HttpGet]
        public async Task<IActionResult> Create(int? eventoId)
        {
            var pagoVM = new PagoVM
            {
                Fecha = System.DateTime.Now,
                Valido = true
            };

            if (eventoId.HasValue)
            {
                // VALIDACIÓN CRÍTICA: Bloquear si ya está saldado
                var eventoVM = await _eventoService.GetByIdAsync(eventoId.Value);
                if (eventoVM == null) return NotFound();

                // Si no debe nada y no está cancelado, redirigir
                if (eventoVM.SaldoRestante <= 0 && eventoVM.Estado != EventoEstado.Cancelado)
                {
                    TempData["Info"] = "Este evento ya se encuentra totalmente saldado.";
                    // Redirigimos al historial de pagos de ese evento para que vea que ya pagó todo
                    return RedirectToAction("Index", new { eventoId = eventoId.Value });
                }

                pagoVM.EventoId = eventoId.Value;
                ViewData["EventoId"] = eventoId.Value;

                float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));
                string eventoDescripcion = $"{eventoVM.ClienteNombreCompleto} - {eventoVM.Tipo} - {eventoVM.Inicio:dd/MM/yyyy}";

                ViewData["EventoDescripcion"] = eventoDescripcion;
                ViewData["SaldoAnterior"] = (float)eventoVM.SaldoRestante;
                ViewData["TotalPagadoActual"] = (float)eventoVM.TotalPagado;
                ViewData["CostoTotalEvento"] = costoTotal;
                ViewData["EstadoActual"] = eventoVM.Estado.ToString();

                // Sugerir el saldo restante como monto a pagar
                pagoVM.Monto = (float)(eventoVM.SaldoRestante);
            }
            else
            {
                // Si entra sin evento preseleccionado, mostrar solo los que deben plata
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }

            return View(pagoVM);
        }

        // --- CREAR (CREATE) POST ---
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

            // Validaciones de negocio
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

            // Validar monto excesivo
            if (pagoVM.Monto > (float)(evento.SaldoRestante))
            {
                ModelState.AddModelError("Monto", $"El monto no puede superar el saldo restante ({evento.SaldoRestante:C2}).");
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

        // --- ANULAR PAGO ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id)
        {
            var pagoExistente = await _pagoService.GetByIdAsync(id);
            if (pagoExistente == null) return NotFound();

            var result = await _pagoService.AnularPagoAsync(id);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                string msgError = result.Errors != null && result.Errors.Any()
                                  ? string.Join(", ", result.Errors)
                                  : result.Message;
                TempData["Error"] = msgError;
            }

            return RedirectToAction("Index", new { eventoId = pagoExistente.EventoId });
        }

        // --- DESCARGAR RECIBO (PDF) ---
        [HttpGet]
        public async Task<IActionResult> DescargarRecibo(int pagoId)
        {
            var pdfBytes = await _pagoService.GenerarReciboPdfAsync(pagoId);
            if (pdfBytes == null)
            {
                return NotFound("No se encontró el pago.");
            }
            string nombreArchivo = $"Recibo-Pago-{pagoId}-{System.DateTime.Now:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", nombreArchivo);
        }

        // --- EXPORTAR A EXCEL (RF-20 para Pagos) ---
        [HttpGet]
        public async Task<IActionResult> ExportarExcel(int? eventoId, string q)
        {
            // Reutilizamos la lógica de filtros pero traemos TODO (sin paginación)
            List<PagoVM> pagos;
            if (eventoId.HasValue)
            {
                pagos = await _pagoService.GetPagosByEventoIdAsync(eventoId.Value);
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
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Pagos");

                // Encabezados
                worksheet.Cell(1, 1).Value = "Fecha";
                worksheet.Cell(1, 2).Value = "Cliente";
                worksheet.Cell(1, 3).Value = "Evento";
                worksheet.Cell(1, 4).Value = "Monto";
                worksheet.Cell(1, 5).Value = "Método";
                worksheet.Cell(1, 6).Value = "Estado";

                var header = worksheet.Range("A1:F1");
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightGray;

                int row = 2;
                foreach (var p in pagos)
                {
                    worksheet.Cell(row, 1).Value = p.Fecha;
                    worksheet.Cell(row, 2).Value = p.ClienteNombre;
                    worksheet.Cell(row, 3).Value = p.EventoDescripcion;
                    worksheet.Cell(row, 4).Value = p.Monto;
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "$ #,##0.00";
                    worksheet.Cell(row, 5).Value = p.Metodo.ToString();
                    worksheet.Cell(row, 6).Value = p.Valido ? "Válido" : "Anulado";

                    if (!p.Valido) worksheet.Row(row).Style.Font.FontColor = XLColor.Red;

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Pagos_{System.DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // --- MÉTODOS PRIVADOS ---
        private async Task PrepararDropdownEventosAsync(int eventoId)
        {
            if (eventoId == 0)
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }
        }

        // --- LOGICA DE CREACIÓN DESDE RESERVA (WIZARD) ---
        [HttpGet]
        public IActionResult CreateReserva()
        {
            if (TempData["PendingPaymentDetails"] is not string paymentJson ||
                TempData["PendingEvent"] is not string eventJson)
            {
                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }

            var pagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);
            var eventoVm = JsonConvert.DeserializeObject<EventoVM>(eventJson);

            float costoTotal = (float)(eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0));
            ViewData["CostoTotalEvento"] = costoTotal;
            ViewData["ObsReserva"] = "Por Reserva";
            ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)";

            TempData.Keep("PendingEvent");
            TempData.Keep("PendingPaymentDetails");

            return View(pagoVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReserva(PagoReservaVM pagoVm)
        {
            if (TempData["PendingEvent"] is not string eventJson ||
                TempData["PendingPaymentDetails"] is not string paymentJson)
            {
                TempData["Error"] = "La sesión ha expirado.";
                return RedirectToAction("Create", "Evento");
            }

            var eventoVm = JsonConvert.DeserializeObject<EventoVM>(eventJson);
            var originalPagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);

            if (!ModelState.IsValid)
            {
                // Restaurar datos
                pagoVm.Monto = originalPagoVm.Monto;
                pagoVm.Fecha = originalPagoVm.Fecha;
                pagoVm.Observaciones = originalPagoVm.Observaciones;

                TempData.Keep("PendingEvent");
                TempData.Keep("PendingPaymentDetails");

                ViewData["CostoTotalEvento"] = eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0);
                ViewData["ObsReserva"] = "Por Reserva";
                ViewData["ObsCompleto"] = "Pago completo del salón (Evento < 48hs)";

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
                    ModelState.AddModelError(string.Empty, error);

                TempData.Keep("PendingEvent");
                TempData.Keep("PendingPaymentDetails");

                // Restaurar vista
                pagoVm.Monto = originalPagoVm.Monto;
                ViewData["CostoTotalEvento"] = eventoVm.CostoAlquiler + (eventoVm.MontoAireAcondicionado ?? 0);

                return View(pagoVm);
            }
        }

        public IActionResult CancelCreate()
        {
            TempData.Remove("PendingEvent");
            TempData.Remove("PendingPaymentDetails");
            TempData["Info"] = "Creación de evento cancelada.";
            return RedirectToAction("Index", "Evento");
        }
    }
}