using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IReporteService
    {
        /// <summary>
        /// Obtiene todos los reportes consolidados para el dashboard
        /// </summary>
        Task<ReportesVM> GetReportesConsolidadosAsync();
        
        /// <summary>
        /// Obtiene el reporte de eventos con comparativas
        /// </summary>
        Task<ReporteEventosVM> GetReporteEventosAsync();
        
        /// <summary>
        /// Obtiene el reporte de ingresos con comparativas
        /// </summary>
        Task<ReporteIngresosVM> GetReporteIngresosAsync();
        
        /// <summary>
        /// Obtiene el reporte de pagos
        /// </summary>
        Task<ReportePagosVM> GetReportePagosAsync();
        
        /// <summary>
        /// Obtiene el reporte de fianzas
        /// </summary>
        Task<ReporteFianzasVM> GetReporteFianzasAsync();
        
        /// <summary>
        /// Obtiene el reporte de clientes
        /// </summary>
        Task<ReporteClientesVM> GetReporteClientesAsync();
    }
}