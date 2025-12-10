using System;
using System.Collections.Generic;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class ReporteCompletoViewModel
    {
        public string TituloReporte { get; set; } = "Ficha de Estado de Evento";
        public DateTime FechaGeneracion { get; set; }

        // --- Datos del Evento y Cliente (RF-01, RF-07) ---
        public string NombreCliente { get; set; }
        public string CI_RUT { get; set; }
        public string LabelDocumento { get; set; }
        public string TelefonoCliente { get; set; }
        public string TipoEvento { get; set; }
        public string FechaEvento { get; set; }
        public string FechaContrato { get; set; }
        public string Horario { get; set; }
        public int CantidadInvitados { get; set; }

        // --- Datos del Responsable del Salón (RF-07) ---
        public string ResponsableNombre { get; set; }
        public string ResponsableTelefono { get; set; }
        public string ResponsableCI { get; set; }

        // --- Finanzas (RF-19 y RF-11) ---
        public decimal CostoAlquilerBase { get; set; }
        public decimal CostoAireAcondicionado { get; set; }
        public decimal TotalGeneral { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string ObservacionesFianza { get; set; }

        // --- Fianza (RF-18) ---
        public decimal MontoFianza { get; set; }
        public string EstadoFianza { get; set; }

        // --- Historial (RF-13) ---
        public List<DetallePagoDTO> HistorialPagos { get; set; } = new List<DetallePagoDTO>();
    }

    /// <summary>
    /// DTO auxiliar para listar los pagos dentro del reporte
    /// </summary>
    public class DetallePagoDTO
    {
        public string Fecha { get; set; }
        public string Metodo { get; set; }
        public decimal Monto { get; set; }
        public string Observacion { get; set; }
        public bool EsAnulado { get; set; }
    }
}