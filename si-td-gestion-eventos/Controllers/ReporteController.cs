using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Controllers
{
    public class ReporteController : Controller
    {
        private readonly IReporteService _reporteService;

        public ReporteController(IReporteService reporteService)
        {
            _reporteService = reporteService;
        }

        // GET: Reporte/Index
        public async Task<IActionResult> Index()
        {
            var reportes = await _reporteService.GetReportesConsolidadosAsync();
            return View(reportes);
        }
    }
}