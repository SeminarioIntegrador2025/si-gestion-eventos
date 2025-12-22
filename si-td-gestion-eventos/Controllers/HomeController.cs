using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Models; // Asegúrate de tener este using para ErrorViewModel

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
                // 1. MANTENIMIENTO AUTOMÁTICO (NUEVO)
                // Ejecutamos la limpieza de eventos pasados (Cierra pagos / Marca deudas)
                // antes de cargar cualquier dato, para que los reportes sean exactos.
                await _eventoService.ActualizarEstadosEventosPasadosAsync();

                // 2. Obtener reportes consolidados (ya con datos actualizados)
                var reportes = await _reporteService.GetReportesConsolidadosAsync();

                // 3. Obtener alertas (Trae futuros + pasados con deuda)
                var alertas = await _eventoService.GetAlertasServiciosAsync();

                // 4. Obtener próximos 5 eventos
                // (Usando lógica de fecha >= Hoy para incluir eventos del día en curso)
                var proximosEventos = await _eventoService.GetLatestAsync(5);

                ViewBag.Reportes = reportes;
                ViewBag.ProximosEventos = proximosEventos;
                ViewBag.AlertasServicios = alertas;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el dashboard");
                // Es buena práctica devolver una vista aunque falle la carga de datos
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