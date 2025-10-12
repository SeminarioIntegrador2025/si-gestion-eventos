using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Entities
{
    public class Fianza
    {
        public int FianzaId { get; set; }
        public required int EventoId { get; set; }
                
        public required decimal Monto { get; set; }
        public required EstadoFianza Estado { get; set; } = EstadoFianza.Registrada;
        public required DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
        public required DateTime FechaDevolucion { get; set; }
        public required decimal MontoDevuelto { get; set; }
        public string? Observaciones { get; set; }

        //relaciones con otros modelos
        public required Evento Evento { get; set; } = null!;

    }
}
