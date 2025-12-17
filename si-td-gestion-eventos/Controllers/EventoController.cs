using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Models.Enums;
using System;
using ClosedXML.Excel;
using System.Collections.Generic; 
using System.Linq; 
using System.Threading.Tasks; 

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController : Controller
    {
        private readonly IEventoService _eventoService;
        private readonly IClienteService _clienteService;
        private readonly IValidator<EventoVM> _validator;

        public EventoController(
            IEventoService eventoService,
            IClienteService clienteService,
            IValidator<EventoVM> validator)
        {
            _eventoService = eventoService;
            _clienteService = clienteService;
            _validator = validator;
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

            // --- Listas para los Dropdowns ---
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
            // 1. Validar con FluentValidation (reglas de formato, fechas lógicas, etc.)
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

            // 2. Calcular la lógica de 48hs (para las sugerencias)
            var deadline = DateTime.Now.AddHours(48);
            var now = DateTime.Now;
            DateTime fechaInicioReal = eventoVM.Inicio.Date.Add(eventoVM.HoraInicio);
            bool esDentroDe48Hs = (fechaInicioReal < deadline && fechaInicioReal > now);

            // 3. Preparar las "Sugerencias" para el Paso 2
            string observacionSugerida;
            float montoSugerido;

            if (esDentroDe48Hs)
            {
                // Si es < 48hs, SIEMPRE sugerimos el pago total
                observacionSugerida = "Pago completo del salón (Evento < 48hs)";
                float costoTotal = (float)(eventoVM.CostoAlquiler + (eventoVM.MontoAireAcondicionado ?? 0));
                montoSugerido = costoTotal;
            }
            else
            {
                // Si es lejano, sugerimos la reserva normal
                observacionSugerida = "Por Reserva";
                montoSugerido = (float)eventoVM.MontoReserva;
            }

            // 4. Comprobar el ModelState (SOLO de FluentValidation)
            if (ModelState.IsValid)
            {
                // 5. Redirigir al Paso 2, pasando las SUGERENCIAS
                TempData["PendingEvent"] = JsonConvert.SerializeObject(eventoVM);

                var pagoVm = new PagoReservaVM
                {
                    Monto = montoSugerido, // <-- Pasa el monto sugerido
                    Fecha = eventoVM.FechaContrato,
                    Observaciones = observacionSugerida // <-- Pasa la observación sugerida
                };
                TempData["PendingPaymentDetails"] = JsonConvert.SerializeObject(pagoVm);

                return RedirectToAction("CreateReserva", "Pago");
            }

            // Si FluentValidation falló (ej. faltó un cliente)
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

        // POST: Evento/Reprogramar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reprogramar(ReprogramarEventoVM model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Datos inválidos para reprogramar.";
                return RedirectToAction("Details", new { id = model.EventoId });
            }

            var result = await _eventoService.ReprogramarAsync(model);

            if (result.Success)
            {
                TempData["Ok"] = result.Message;
                // Opcional: Redirigir a EDIT para que ajuste los costos si es necesario, 
                // ya que el requerimiento dice "habilitar edición de campos".
                // return RedirectToAction("Edit", new { id = model.EventoId });

                // O simplemente volver al detalle:
                return RedirectToAction("Details", new { id = model.EventoId });
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors);
                return RedirectToAction("Details", new { id = model.EventoId });
            }
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


        [HttpGet]
        public async Task<IActionResult> ExportarExcel(string? q, DateTime? fechaDesde, DateTime? fechaHasta, EventoEstado? estado)
        {
            // 1. OBTENER DATOS
            // Reutiliza tu lógica de filtros pero trae TODO (sin paginación)
            // Si no tienes un método específico, usa el de paginación con un PageSize alto
            var eventos = await _eventoService.ObtenerTodosFiltradosAsync(q, fechaDesde, fechaHasta, estado);

            // 2. GENERAR EXCEL
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Listado de Eventos");

                // --- ESTILOS ---
                var headerStyle = workbook.Style;
                headerStyle.Font.Bold = true;
                headerStyle.Fill.BackgroundColor = XLColor.LightGray;
                headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // --- TÍTULO ---
                worksheet.Cell(1, 1).Value = "Reporte General de Eventos";
                worksheet.Range("A1:G1").Merge().Style.Font.FontSize = 14;
                worksheet.Range("A1:G1").Style.Font.Bold = true;

                worksheet.Cell(2, 1).Value = $"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}";

                // --- ENCABEZADOS (Fila 4) ---
                int headerRow = 4;
                worksheet.Cell(headerRow, 1).Value = "ID";
                worksheet.Cell(headerRow, 2).Value = "Cliente";
                worksheet.Cell(headerRow, 3).Value = "Tipo Evento";
                worksheet.Cell(headerRow, 4).Value = "Fecha Inicio";
                worksheet.Cell(headerRow, 5).Value = "Estado";
                worksheet.Cell(headerRow, 6).Value = "Costo Total";
                worksheet.Cell(headerRow, 7).Value = "Saldo Pendiente";

                worksheet.Range(headerRow, 1, headerRow, 7).Style = headerStyle;

                // --- DATOS ---
                int row = 5;
                foreach (var item in eventos)
                {
                    worksheet.Cell(row, 1).Value = item.EventoId;
                    worksheet.Cell(row, 2).Value = item.ClienteNombreCompleto;
                    worksheet.Cell(row, 3).Value = item.Tipo.ToString();
                    worksheet.Cell(row, 4).Value = item.Inicio;
                    worksheet.Cell(row, 5).Value = item.Estado.ToString();

                    // CORRECCIÓN 1: Usamos la propiedad calculada que agregaremos en el Paso 2
                    worksheet.Cell(row, 6).Value = item.CostoTotal;
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "$ #,##0.00";

                    // CORRECCIÓN 2: Cambiamos 'SaldoPendiente' por 'SaldoRestante'
                    worksheet.Cell(row, 7).Value = item.SaldoRestante;
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "$ #,##0.00";

                    // Usamos 'SaldoRestante' para la condición también
                    if (item.SaldoRestante > 0)
                    {
                        worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                    }

                    row++;
                }

                // Autoajustar columnas
                worksheet.Columns().AdjustToContents();

                // 3. RETORNAR
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

        // --- Métodos Helper ---

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