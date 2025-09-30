using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Models
{
    public class Fianza
    {
        public int Id { get; set; }
        public int EventoId { get; set; }
        public Evento Evento { get; set; } = null!;

        public decimal Monto { get; set; }
        public EstadoFianza Estado { get; set; } = EstadoFianza.Registrada;
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
        public DateTime? FechaDevolucion { get; set; }
        public decimal? MontoDevuelto { get; set; }
        public string? Observaciones { get; set; }

    }
}
