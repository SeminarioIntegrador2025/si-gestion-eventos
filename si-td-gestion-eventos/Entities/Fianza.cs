using si_td_gestion_eventos.Models.Enums;
using System.ComponentModel.DataAnnotations; // (Necesario para 'Required')

namespace si_td_gestion_eventos.Entities
{
    public class Fianza
    {
        public int FianzaId { get; set; }

        [Required]
        public int EventoId { get; set; }

        [Required]
        public decimal Monto { get; set; } 

        [Required]
        public EstadoFianza Estado { get; set; } = EstadoFianza.Registrada;

        [Required]
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
        public DateTime? FechaDevolucion { get; set; }
        public decimal? MontoDevuelto { get; set; }
        public string? Observaciones { get; set; }
        [Required]
        public Evento Evento { get; set; } = null!;
    }
}