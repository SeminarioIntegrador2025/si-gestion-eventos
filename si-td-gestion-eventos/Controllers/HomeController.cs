using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IReporteService _reporteService;
        private readonly IEventoService _eventoService;

        public HomeController(
            ILogger<HomeController> logger,
            IReporteService reporteService,
            IEventoService eventoService)
        {
            _logger = logger;
            _reporteService = reporteService;
            _eventoService = eventoService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Obtener reportes consolidados
                var reportes = await _reporteService.GetReportesConsolidadosAsync();
                
                // Obtener próximos 5 eventos usando el método existente
                var proximosEventos = await _eventoService.GetLatestAsync(5);
                
                ViewBag.Reportes = reportes;
                ViewBag.ProximosEventos = proximosEventos;
                
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el dashboard");
                return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
