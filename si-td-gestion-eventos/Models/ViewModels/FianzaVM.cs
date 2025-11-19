using si_td_gestion_eventos.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Models.ViewModels
{
    // Este VM ahora sirve para AMBAS vistas: Create (formulario) e Index (lista)
    public class FianzaVM
    {
        // --- Propiedades del Formulario ---
        public int FianzaId { get; set; }

        [Required]
        public int EventoId { get; set; }
        public string? EventoDescripcion { get; set; } // Para el 'Create'

        [Required(ErrorMessage = "Debe ingresar un monto de fianza.")]
        [Display(Name = "Monto de la Fianza")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "La fecha de registro es obligatoria.")]
        [Display(Name = "Fecha de Registro")]
        [DataType(DataType.Date)]
        public DateTime FechaRegistro { get; set; } = DateTime.Today;

        public string? Observaciones { get; set; }

        // --- Propiedades ADICIONALES (para la lista 'Index') ---
        // (Estas son las que faltan y causan los errores)

        [Display(Name = "Cliente")]
        public string? ClienteNombre { get; set; }

        [Display(Name = "Fecha del Evento")]
        [DataType(DataType.Date)]
        public DateTime EventoFecha { get; set; }

        [Display(Name = "Estado")]
        public EstadoFianza Estado { get; set; }

        [Display(Name = "Monto Devuelto")]
        [DataType(DataType.Currency)]
        public decimal? MontoDevuelto { get; set; }

        [Display(Name = "Fecha de Devolución")]
        [DataType(DataType.Date)]
        public DateTime? FechaDevolucion { get; set; }
    }
}