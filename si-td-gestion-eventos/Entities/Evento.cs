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
        public EventoEstado Estado { get; set; } = EventoEstado.PendienteAdeudado;

        public String? Observaciones { get; set; }

        // Propiedad Adueñada (Owned) para Responsable
        [Required]
        public ResponsableSalon ResponsableSalon { get; set; } = null!;

        // Relaciones
        [Required]
        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; } = null!;
        public virtual ICollection<ServicioEsencial> ServiciosEsenciales { get; set; } = new List<ServicioEsencial>();
        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
        public int? FianzaId { get; set; }
        public Fianza? Fianza { get; set; }
        public ICollection<Reporte> Reportes { get; set; } = new List<Reporte>();
        public string ResponsableNombre { get; set; }
        public string ResponsableTelefono { get; set; }
        public string ResponsableCedula { get; set; }
    }
}