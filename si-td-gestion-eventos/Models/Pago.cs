using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Models
{
    public class Pago
    {
        public int Id { get; set; }
        public required DateTime Fecha { get; set; }
        public required decimal Monto { get; set; }
        public required MetodoPago Metodo { get; set; }
        public string? ComprobanteRuta { get; set; }
        public TipoArchivo? ComprobanteTipo { get; set; }

        //relaciones con otros modelos
        public required Evento Evento { get; set; } = null!;
    }
}
