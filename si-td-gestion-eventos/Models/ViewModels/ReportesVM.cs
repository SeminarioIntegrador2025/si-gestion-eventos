namespace si_td_gestion_eventos.Models.ViewModels
{
    /// <summary>
    /// ViewModel principal que contiene todos los reportes del dashboard
    /// </summary>
    public class ReportesVM
    {
        public ReporteEventosVM Eventos { get; set; } = new();
        public ReporteIngresosVM Ingresos { get; set; } = new();
        public ReportePagosVM Pagos { get; set; } = new();
        public ReporteFianzasVM Fianzas { get; set; } = new();
        public ReporteClientesVM Clientes { get; set; } = new();
        public List<TopClienteVM> TopClientes { get; set; } = new();
        public List<EventoPorTipoVM> EventosPorTipo { get; set; } = new();
    }

    /// <summary>
    /// Reporte de eventos con comparativas mensuales
    /// </summary>
    public class ReporteEventosVM
    {
        // Mes actual
        public int EventosEsteMes { get; set; }
        public int EventosRealizadosEsteMes { get; set; }
        public int EventosPendientesEsteMes { get; set; }
        public int EventosCanceladosEsteMes { get; set; }

        // Mes anterior
        public int EventosMesAnterior { get; set; }
        
        // Comparativas
        public decimal PorcentajeCambioEventos { get; set; }
        public bool CrecimientoPositivo { get; set; }

        // Proyecciones
        public int EventosProximoMes { get; set; }
        public decimal TasaOcupacionEsteMes { get; set; }
        public int DiasDisponiblesEsteMes { get; set; }

        // Ocupación de fines de semana
        public decimal TasaOcupacionFinesDeSemana { get; set; }
        public int FinesDeSemanaOcupados { get; set; }
        public int TotalFinesDeSemanaDisponibles { get; set; }
    }

    /// <summary>
    /// Reporte de ingresos con comparativas (basado en pagos recibidos)
    /// </summary>
    public class ReporteIngresosVM
    {
        // Mes actual (pagos recibidos)
        public decimal TotalIngresosEsteMes { get; set; }
        public decimal TotalReservasEsteMes { get; set; }
        public decimal TotalAlquileresEsteMes { get; set; }
        public decimal TotalAireAcondicionadoEsteMes { get; set; }

        // Mes anterior
        public decimal TotalIngresosMesAnterior { get; set; }

        // Comparativas
        public decimal PorcentajeCambioIngresos { get; set; }
        public bool CrecimientoPositivo { get; set; }

        // Año en curso
        public decimal TotalIngresosAnioActual { get; set; }
        public decimal PromedioIngresosMensual { get; set; }

        // Desglose por concepto
        public decimal PorcentajeReservas { get; set; }
        public decimal PorcentajeAlquileres { get; set; }
        public decimal PorcentajeAireAcondicionado { get; set; }
    }

    /// <summary>
    /// Reporte de pagos con métodos y tendencias
    /// </summary>
    public class ReportePagosVM
    {
        // Totales mes actual
        public int TotalPagosEsteMes { get; set; }
        public decimal MontoTotalPagadoEsteMes { get; set; }

        // Por método de pago
        public int PagosEfectivoEsteMes { get; set; }
        public decimal MontoEfectivoEsteMes { get; set; }
        public int PagosTransferenciaEsteMes { get; set; }
        public decimal MontoTransferenciaEsteMes { get; set; }

        // Comparativas
        public int TotalPagosMesAnterior { get; set; }
        public decimal PorcentajeCambioPagos { get; set; }

        // Cuentas por cobrar
        public decimal TotalAdeudado { get; set; }
        public int EventosConAdeudo { get; set; }
        public decimal PorcentajeRecuperacion { get; set; }
    }

    /// <summary>
    /// Reporte de fianzas
    /// </summary>
    public class ReporteFianzasVM
    {
        // Totales
        public decimal TotalFianzasRegistradas { get; set; }
        public decimal TotalFianzasDevueltas { get; set; }
        public decimal TotalFianzasPendientesDevolucion { get; set; }

        // Mes actual
        public int FianzasRegistradasEsteMes { get; set; }
        public int FianzasDevueltasEsteMes { get; set; }
        
        // Estadísticas
        public decimal PorcentajeDevolucionTotal { get; set; }
        public decimal MontoPromedioFianza { get; set; }
        public int FianzasVencidas { get; set; }
    }

    /// <summary>
    /// Reporte de clientes
    /// </summary>
    public class ReporteClientesVM
    {
        public int TotalClientes { get; set; }
        public int ClientesActivos { get; set; }
        public int ClientesInactivos { get; set; }
        public int NuevosClientesEsteMes { get; set; }
        public int ClientesRecurrentesEsteMes { get; set; }
        public decimal PorcentajeRecurrencia { get; set; }
        public decimal PromedioEventosPorCliente { get; set; }
    }

    /// <summary>
    /// Top clientes por ingresos generados
    /// </summary>
    public class TopClienteVM
    {
        public int ClienteId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public int TotalEventos { get; set; }
        public decimal TotalIngresos { get; set; }
        public decimal PromedioGasto { get; set; }
        public DateTime UltimoEvento { get; set; }
    }

    /// <summary>
    /// Distribución de eventos por tipo
    /// </summary>
    public class EventoPorTipoVM
    {
        public string TipoEvento { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PorcentajeDelTotal { get; set; }
        public decimal IngresoTotal { get; set; }
    }
}