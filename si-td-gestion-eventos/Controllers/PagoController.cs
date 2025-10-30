
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Para SelectList
using Newtonsoft.Json;
using si_td_gestion_eventos.Models.Enums;  // Para los KPIs
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

        // /Pago (global) Y /Pago?eventoId=5 (filtrado)  (Para seguir el principio DRY (Don repeat yourself))
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
                        (p.ReferenciaComprobante != null && p.ReferenciaComprobante.ToLower().Contains(lowerQ)) ||
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

        // --- MÉTODO CREATE (GET) ---
        // Maneja /Pago/Create Y /Pago/Create?eventoId=5
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
            }
            else
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }

            return View(pagoVM);
        }

        // --- MÉTODO CREATE (POST - MODIFICADO) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PagoVM pagoVM)
        {

            if (!ModelState.IsValid)
            {
                await PrepararDropdownEventosAsync(pagoVM.EventoId); // (Llama al helper)
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
                // Falla del servicio. Agrega los errores.
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await PrepararDropdownEventosAsync(pagoVM.EventoId);
                return View(pagoVM);
            }
        }

        // GET: /Pago/DescargarRecibo
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

        private async Task PrepararDropdownEventosAsync(int eventoId)
        {
            // Solo recargamos el dropdown si el EventoId es 0 (formulario global)
            if (eventoId == 0)
            {
                ViewBag.Eventos = await _eventoService.GetEventosAdeudadosParaDropdownAsync();
            }
        }
        // GET: Pago/CreateReserva (Llamado desde EventoController)
        [HttpGet]
        public IActionResult CreateReserva()
        {
            if (TempData["PendingPaymentDetails"] is not string paymentJson)
            {

                TempData["Error"] = "Sesión expirada. Inicie de nuevo.";
                return RedirectToAction("Create", "Evento");
            }

            var pagoVm = JsonConvert.DeserializeObject<PagoReservaVM>(paymentJson);

     
            TempData.Keep("PendingEvent");
            TempData.Keep("PendingPaymentDetails");

            return View(pagoVm);
        }

        // POST: Pago/CreateReserva
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
                // 5. Si la transacción falla (ej. error de DB), mostrar error
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                TempData.Keep("PendingEvent");
                TempData.Keep("PendingPaymentDetails");

                pagoVm.Monto = originalPagoVm.Monto;
                pagoVm.Fecha = originalPagoVm.Fecha;
                pagoVm.Observaciones = originalPagoVm.Observaciones;

                return View(pagoVm); 
            }
        }

        // GET: Pago/CancelCreate

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