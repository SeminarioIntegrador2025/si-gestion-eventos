using si_td_gestion_eventos.Models.Enums; // Para MetodoPago, etc.

namespace si_td_gestion_eventos.Entities
{
    public class Pago
    {
        public int PagoId { get; set; }
        public DateTime Fecha { get; set; }
        public float Monto { get; set; }
        public MetodoPago Metodo { get; set; }
        public string? Observaciones { get; set; }

        // --- ARREGLO 1: Propiedades de Relación ---
        public int EventoId { get; set; } // <-- La propiedad que faltaba
        public Evento Evento { get; set; }

        public int? ComprobanteExternoId { get; set; } // Para la relación 1-a-1
        public ComprobanteExterno? ComprobanteExterno { get; set; }
    }
}