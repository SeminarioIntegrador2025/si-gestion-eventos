using System.ComponentModel.DataAnnotations;
using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class EventoVM
    {
        public int EventoId { get; set; }
        // ... otras propiedades del VM ...
        public DateTime FechaContrato { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime Fin { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public TipoEvento Tipo { get; set; }
        public decimal CostoAlquiler { get; set; }
        public decimal MontoReserva { get; set; }
        public int CantidadPersonas { get; set; }
        public decimal? MontoAireAcondicionado { get; set; }
        public EventoEstado Estado { get; set; }

        // --- CAMPO PARA SELECCIONAR EL CLIENTE ---
        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        [Display(Name = "Cliente (Contratante)")]
        public int ClienteId { get; set; }

        // --- CAMPOS PARA LOS DATOS DEL RESPONSABLE (VUELVEN A AGREGARSE) ---
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

        // --- Propiedades de solo lectura para mostrar información ---
        public string? ClienteNombreCompleto { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal SaldoRestante { get; set; }
    }
}