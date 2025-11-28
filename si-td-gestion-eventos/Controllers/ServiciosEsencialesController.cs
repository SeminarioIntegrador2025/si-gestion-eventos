using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System.Threading.Tasks;

namespace si_td_gestion_eventos.Controllers
{
    public class ServiciosEsencialesController : Controller
    {
        private readonly IServicioEsencialService _servicioEsencial;

        public ServiciosEsencialesController(IServicioEsencialService servicioEsencial)
        {
            _servicioEsencial = servicioEsencial;
        }

        // POST: Subir Comprobante
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subir(int id, ServicioEsencialVM modelo)
        {
            if (modelo.ArchivoSubido == null || modelo.ArchivoSubido.Length == 0)
            {
                TempData["Error"] = "Debe seleccionar un archivo.";
                return RedirectToAction("Details", "Evento", new { id = modelo.EventoId });
            }

            var result = await _servicioEsencial.SubirComprobanteAsync(id, modelo);

            if (result.Success)
            {
                TempData["Ok"] = "Comprobante subido y verificado correctamente.";
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            // Redirigimos siempre al detalle del EVENTO padre
            return RedirectToAction("Details", "Evento", new { id = modelo.EventoId });
        }

        // POST: Eliminar Comprobante
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id, int eventoId)
        {
            var result = await _servicioEsencial.EliminarComprobanteAsync(id);

            if (result.Success)
            {
                TempData["Ok"] = "Comprobante eliminado. El estado ha vuelto a Pendiente.";
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("Details", "Evento", new { id = eventoId });
        }
    }
}