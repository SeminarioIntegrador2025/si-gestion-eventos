using System.ComponentModel.DataAnnotations;
using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class EventoVM
    {
        public int EventoId { get; set; }
        
        [Required(ErrorMessage = "La fecha de contrato es obligatoria.")]
        [Display(Name = "Fecha de Contrato")]
        public DateTime FechaContrato { get; set; }
        
        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        [Display(Name = "Fecha de Inicio")]
        public DateTime Inicio { get; set; }
        
        [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
        [Display(Name = "Fecha de Fin")]
        public DateTime Fin { get; set; }
        
        [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
        [Display(Name = "Hora de Inicio")]
        public TimeSpan HoraInicio { get; set; }
        
        [Required(ErrorMessage = "La hora de fin es obligatoria.")]
        [Display(Name = "Hora de Fin")]
        public TimeSpan HoraFin { get; set; }
        
        [Required(ErrorMessage = "El tipo de evento es obligatorio.")]
        [Display(Name = "Tipo de Evento")]
        public TipoEvento? Tipo { get; set; }
        
        [Required(ErrorMessage = "El costo de alquiler es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El costo debe ser mayor a 0.")]
        [Display(Name = "Costo de Alquiler")]
        public decimal CostoAlquiler { get; set; }
        
        [Required(ErrorMessage = "El monto de reserva es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto de reserva debe ser mayor a 0.")]
        [Display(Name = "Monto de Reserva")]
        public decimal MontoReserva { get; set; }
        
        [Required(ErrorMessage = "La cantidad de personas es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe haber al menos 1 persona.")]
        [Display(Name = "Cantidad de Personas")]
        public int CantidadPersonas { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "El monto del aire acondicionado no puede ser negativo.")]
        [Display(Name = "Monto Aire Acondicionado")]
        public decimal? MontoAireAcondicionado { get; set; }
        
        [Display(Name = "Estado")]
        public EventoEstado Estado { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        [Display(Name = "Cliente (Contratante)")]
        public int ClienteId { get; set; }

        [Display(Name = "Tipo de Cliente")]
        public TipoCliente TipoCli { get; set; }


        [Required(ErrorMessage = "El nombre del responsable es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombre del Responsable (Contacto en el evento)")]
        public string ResponsableNombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono del responsable es obligatorio.")]
        [StringLength(30)]
        [Display(Name = "Teléfono del Responsable")]
        public string ResponsableTelefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "La cédula del responsable es obligatoria.")]
        [StringLength(30)]
        [Display(Name = "Cédula del Responsable")]
        public string ResponsableCedula { get; set; } = string.Empty;

        public string? Observaciones { get; set; } = string.Empty;

        public string? OrdenPor { get; set; }

        public FianzaVM? DetalleFianza { get; set; }
        // Propiedades de solo lectura para la vista
        public string? ClienteNombreCompleto { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal SaldoRestante { get; set; }
    }
}