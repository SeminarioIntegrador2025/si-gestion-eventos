using si_td_gestion_eventos.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace si_td_gestion_eventos.Entities
{
    public class Evento
    {
        public int EventoId { get; set; }

        [Required]
        public DateTime FechaContrato { get; set; }

        [Required]
        public DateTime Inicio { get; set; }

        [Required]
        public DateTime Fin { get; set; }

        [Required]
        public TimeSpan HoraInicio { get; set; }

        [Required]
        public TimeSpan HoraFin { get; set; }

        [Required]
        public TipoEvento Tipo { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public float CostoAlquiler { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public float MontoReserva { get; set; }

        [Required]
        public int CantidadPersonas { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public float? MontoAireAcondicionado { get; set; }

        [Required]
        public EventoEstado Estado { get; set; } = EventoEstado.Pendiente;

        public String? Observaciones { get; set; }

        // Propiedad Adueñada (Owned) para Responsable
        [Required]
        public ResponsableSalon ResponsableSalon { get; set; } = null!;

        // Propiedad Adueñada (Owned) para Servicios (ahora solo con AGADU)
        [Required]
        public ServiciosEsenciales ServiciosEsenciales { get; set; } = null!;

        // Relaciones
        [Required]
        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; } = null!;

        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
        public ICollection<Fianza> Fianzas { get; set; } = new List<Fianza>();
        public ICollection<Reporte> Reportes { get; set; } = new List<Reporte>();
    }
}