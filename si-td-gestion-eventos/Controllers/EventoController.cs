using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System;
using System.Collections.Generic;
using System.IO; // Required for MemoryStream
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController : Controller
    {
        private readonly IEventoService _eventoService;
        private readonly IClienteService _clienteService;
        private readonly IValidator<EventoVM> _validator;
        private readonly IValidator<ReprogramarEventoVM> _validatorReprogramar; // Validator for rescheduling

        public EventoController(
            IEventoService eventoService,
            IClienteService clienteService,
            IValidator<EventoVM> validator,
            IValidator<ReprogramarEventoVM> validatorReprogramar)
        {
            _eventoService = eventoService;
            _clienteService = clienteService;
            _validator = validator;
            _validatorReprogramar = validatorReprogramar;
        }

        // GET: Evento/Index
        public async Task<IActionResult> Index(
            string q,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EventoEstado? estado,
            string ordenPor = "fecha_inicio_asc",
            int page = 1,
            int pageSize = 10)
        {
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value < fechaDesde.Value)
            {
                ModelState.AddModelError("Fechas", "La 'Fecha Hasta' no puede ser anterior a la 'Fecha Desde'.");
                ViewBag.Search = q;
                ViewBag.FechaDesde = fechaDesde;
                ViewBag.FechaHasta = fechaHasta;

                ViewBag.EstadosList = new SelectList(Enum.GetValues(typeof(EventoEstado)), estado);
                ViewBag.PageSizeList = new SelectList(new[] { 10, 25, 50 }, pageSize);
                ViewBag.OrdenPorList = new SelectList(GetOrdenPorOpciones(), "Value", "Text", ordenPor);

                var emptyList = new PaginatedList<EventoVM>(new List<EventoVM>(), 0, page, pageSize);
                return View(emptyList);
            }
            var paginatedList = await _eventoService.GetAllPaginatedAsync(
                q,
                fechaDesde,
                fechaHasta,
                estado,
                ordenPor,
                page,
                pageSize);
            ViewBag.Search = q;
            ViewBag.FechaDesde = fechaDesde;
            ViewBag.FechaHasta = fechaHasta;
            ViewBag.Estado = estado;

            // --- Dropdown Lists ---
            ViewBag.EstadosList = new SelectList(Enum.GetValues(typeof(EventoEstado)), estado);
            ViewBag.PageSizeList = new SelectList(new[] { 10, 25, 50 }, pageSize);
            ViewBag.OrdenPorList = new SelectList(GetOrdenPorOpciones(), "Value", "Text", ordenPor);

            return View(paginatedList);
        }

        // GET: Evento/Details/{id} 
        public async Task<IActionResult> Details(int id)
        {
            var eventoVM = await _eventoService.GetByIdAsync(id);
            if (eventoVM is null)
            {
                return NotFound();
            }
            return View(eventoVM);
        }

        // GET: Evento/Create 
        public async Task<IActionResult> Create()
        {
            var viewModel = new EventoVM
            {
                FechaContrato = DateTime.Today,
                Inicio = DateTime.Today.AddDays(1),
                Fin = DateTime.Today.AddDays(1)
            };
            await PopulateClientesDropdown();
            return View(viewModel);
        }

        // POST: Evento/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EventoVM eventoVM)
        {
            // 1. Manual Validation with FluentValidation
            var validationResult = await _validator.ValidateAsync(eventoVM, options =>
            {
                options.IncludeRuleSets("default", "Create");
            });

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
            }

            // 2. 48hs Logic Calculation
            var deadline = DateTime.Now.AddHours(48);
            var now = DateTime.Now;
            DateTime fechaInicioReal = eventoVM.Inicio.Date.Add((TimeSpan)eventoVM.HoraInicio);
            bool esDentroDe48Hs = (fechaInicioReal < deadline && fechaInicioReal > now);

            // 3. Prepare "Suggestions" for Step 2
            string observacionSugerida;
            float montoSugerido;

            if (esDentroDe48Hs)
            {
                observacionSugerida = "Pago completo del salón (Evento < 48hs)";
                float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));
                montoSugerido = costoTotal;
            }
            else
            {
                observacionSugerida = "Por Reserva";
                montoSugerido = (float)eventoVM.MontoReserva;
            }

            // 4. Check ModelState
            if (ModelState.IsValid)
            {
                // 5. Redirect to Step 2
                TempData["PendingEvent"] = JsonConvert.SerializeObject(eventoVM);

                var pagoVm = new PagoReservaVM
                {
                    Monto = montoSugerido,
                    Fecha = eventoVM.FechaContrato,
                    Observaciones = observacionSugerida
                };
                TempData["PendingPaymentDetails"] = JsonConvert.SerializeObject(pagoVm);

                return RedirectToAction("CreateReserva", "Pago");
            }

            // Validation failed
            await PopulateClientesDropdown();
            return View(eventoVM);
        }

        // GET: Evento/Edit/{id} 
        public async Task<IActionResult> Edit(int id)
        {
            var eventoVM = await _eventoService.GetByIdAsync(id);
            if (eventoVM is null)
            {
                return NotFound();
            }
            var canModify = await _eventoService.CanModifyEventoAsync(id);
            ViewBag.CanModify = canModify;
            await PopulateClientesDropdown();
            return View(eventoVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int eventoId, EventoEstado nuevoEstado)
        {
            // Llamamos al servicio para realizar el cambio manual
            var result = await _eventoService.CambiarEstadoManualAsync(eventoId, nuevoEstado);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            // Redireccionamos de vuelta a la vista de detalles
            return RedirectToAction(nameof(Details), new { id = eventoId });
        }

        // POST: Evento/Edit/{id} 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EventoVM eventoVM)
        {
            if (id != eventoVM.EventoId)
            {
                return NotFound();
            }

            var validationResult = await _validator.ValidateAsync(eventoVM, options =>
            {
                options.IncludeRuleSets("default", "Edit");
            });

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
            }

            if (ModelState.IsValid)
            {
                var result = await _eventoService.UpdateAsync(eventoVM);
                if (result.Success)
                {
                    TempData["Ok"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }

            if (!ModelState.IsValid)
            {
                var eventoOriginal = await _eventoService.GetByIdAsync(id);
                if (eventoOriginal != null)
                {
                    eventoVM.ClienteNombreCompleto = eventoOriginal.ClienteNombreCompleto;
                    eventoVM.ClienteId = eventoOriginal.ClienteId;
                }
            }

            await PopulateClientesDropdown();
            return View(eventoVM);
        }

        // POST: Evento/Reprogramar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reprogramar(ReprogramarEventoVM model)
        {
            // --- FIX FOR ASYNC ERROR: MANUAL VALIDATION ---
            // We call ValidateAsync explicitly here instead of relying on AutoValidation
            ValidationResult validationResult = await _validatorReprogramar.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                // Join errors into a string to display in TempData
                var errores = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
                TempData["Error"] = "No se pudo reprogramar: " + errores;

                // Redirect to Details so user sees the error
                return RedirectToAction("Details", new { id = model.EventoId });
            }

            // If valid, call service (which has double-check logic)
            var result = await _eventoService.ReprogramarAsync(model);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                return RedirectToAction("Details", new { id = model.EventoId });
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors);
                return RedirectToAction("Details", new { id = model.EventoId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarExcel(string? q, DateTime? fechaDesde, DateTime? fechaHasta, EventoEstado? estado)
        {
            var eventos = await _eventoService.ObtenerTodosFiltradosAsync(q, fechaDesde, fechaHasta, estado);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Listado de Eventos");

                // --- STYLES ---
                var headerStyle = workbook.Style;
                headerStyle.Font.Bold = true;
                headerStyle.Fill.BackgroundColor = XLColor.LightGray;
                headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // --- TITLE ---
                worksheet.Cell(1, 1).Value = "Reporte General de Eventos";
                worksheet.Range("A1:G1").Merge().Style.Font.FontSize = 14;
                worksheet.Range("A1:G1").Style.Font.Bold = true;

                worksheet.Cell(2, 1).Value = $"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}";

                // --- HEADERS (Row 4) ---
                int headerRow = 4;
                worksheet.Cell(headerRow, 1).Value = "ID";
                worksheet.Cell(headerRow, 2).Value = "Cliente";
                worksheet.Cell(headerRow, 3).Value = "Tipo Evento";
                worksheet.Cell(headerRow, 4).Value = "Fecha Inicio";
                worksheet.Cell(headerRow, 5).Value = "Estado";
                worksheet.Cell(headerRow, 6).Value = "Costo Total";
                worksheet.Cell(headerRow, 7).Value = "Saldo Pendiente";

                worksheet.Range(headerRow, 1, headerRow, 7).Style = headerStyle;

                // --- DATA ---
                int row = 5;
                foreach (var item in eventos)
                {
                    worksheet.Cell(row, 1).Value = item.EventoId;
                    worksheet.Cell(row, 2).Value = item.ClienteNombreCompleto;
                    worksheet.Cell(row, 3).Value = item.Tipo.ToString();
                    worksheet.Cell(row, 4).Value = item.Inicio;
                    worksheet.Cell(row, 5).Value = item.Estado.ToString();

                    worksheet.Cell(row, 6).Value = item.CostoTotal;
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "$ #,##0.00";

                    worksheet.Cell(row, 7).Value = item.SaldoRestante;
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "$ #,##0.00";

                    if (item.SaldoRestante > 0)
                    {
                        worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                    }

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Eventos_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }

        // GET: Evento/Cancel/{id} 
        public async Task<IActionResult> Cancel(int id)
        {
            var eventoVM = await _eventoService.GetByIdAsync(id);
            if (eventoVM is null)
            {
                return NotFound();
            }
            return View(eventoVM);
        }

        // POST: Evento/Cancel/{id} 
        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            var result = await _eventoService.CancelAsync(id);
            if (result.Success)
            {
                TempData["Ok"] = result.Message;
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors);
            }
            return RedirectToAction(nameof(Index));
        }

        // --- Helper Methods ---

        private async Task PopulateClientesDropdown()
        {
            var clientes = await _clienteService.GetClientesActivosParaDropdownAsync();
            ViewBag.Clientes = new SelectList(clientes, "Value", "Text");
        }

        private List<SelectListItem> GetOrdenPorOpciones()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "fecha_inicio_asc", Text = "Fecha de inicio (más cercana)" },
                new SelectListItem { Value = "fecha_inicio_desc", Text = "Fecha de inicio (más lejana)" },
                new SelectListItem { Value = "fecha_contrato_asc", Text = "Fecha de contrato (más cercana)" },
                new SelectListItem { Value = "fecha_contrato_desc", Text = "Fecha de contrato (más lejana)" },
                new SelectListItem { Value = "cantidad_personas_desc", Text = "Cantidad de Personas (Mayor)" },
                new SelectListItem { Value = "cantidad_personas_asc", Text = "Cantidad de Personas (Menor)" },
                new SelectListItem { Value = "tipo_evento", Text = "Tipo de Evento (A-Z)" }
            };
        }
    }
}