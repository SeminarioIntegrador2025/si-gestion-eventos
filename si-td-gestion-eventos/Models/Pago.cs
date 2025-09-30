using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Models
{
    public class Pago
    {
        public int Id { get; set; }
        public int EventoId { get; set; }
        public Evento Evento { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public decimal Monto { get; set; }
        public MetodoPago Metodo { get; set; }
        public string? ComprobanteRuta { get; set; }
        public TipoArchivo? ComprobanteTipo { get; set; }
    }
}
