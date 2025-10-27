using Microsoft.AspNetCore.Http;
using si_td_gestion_eventos.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class PagoVM
    {
        public int PagoId { get; set; }

        [Required(ErrorMessage = "El ID del evento es obligatorio.")]
        public int EventoId { get; set; }
        // Para mostrar info del evento en la vista
        public string? EventoDescripcion { get; set; }
        public string? ClienteNombre { get; set; }

        [Required(ErrorMessage = "La fecha del pago es obligatoria.")]
        [Display(Name = "Fecha de Pago")]
        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "El monto es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        [DataType(DataType.Currency)]
        public float Monto { get; set; } // O decimal

        [Required(ErrorMessage = "Debe seleccionar un método de pago.")]
        [Display(Name = "Método de Pago")]
        public MetodoPago Metodo { get; set; }

        [StringLength(500)]
        public string? Observaciones { get; set; }

        // --- Para Comprobante Adjunto (Transferencia/Externo) ---
        [Display(Name = "Adjuntar Comprobante")]
        public IFormFile? ArchivoComprobante { get; set; } // Para subir el archivo
        public string? RutaArchivoExistente { get; set; } // Para mostrar/borrar el actual
        public TipoArchivo? TipoArchivoComprobante { get; set; } // PDF, JPG, etc.
        public string? ReferenciaComprobante { get; set; } // Ej: Nro Transferencia

        // --- Para Recibo en Efectivo ---
        // (No se sube archivo, se genera)
    }
}