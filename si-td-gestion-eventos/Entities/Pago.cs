using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Entities
{
    public class Pago
    {
        public int PagoId { get; set; }
        public required DateTime Fecha { get; set; }
        public required decimal Monto { get; set; }
        public required MetodoPago Metodo { get; set; }
        public int ComprobanteId { get; set; }
        public string? ComprobanteRuta { get; set; }
        public TipoArchivo? ComprobanteTipo { get; set; }

        //relaciones con otros modelos
        public required Evento Evento { get; set; } = null!;

        public ComprobanteExterno? ComprobanteExterno { get; set; }
    }
}
