// En: Models/ViewModels/PagoReservaVM.cs

using Microsoft.AspNetCore.Http;
using si_td_gestion_eventos.Models.Enums; // <-- Importamos tu Enum
using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class PagoReservaVM
    {

        [Display(Name = "Monto de Reserva")]
        [DataType(DataType.Currency)]
        public float Monto { get; set; } 
        [Display(Name = "Fecha de Contrato")]
        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }

        public string? Observaciones { get; set; }


        // --- Campos a Rellenar por el Usuario ---

        [Display(Name = "Método de Pago")]
        [Required(ErrorMessage = "Debe seleccionar un método de pago.")]
        public MetodoPago Metodo { get; set; } 

        [Display(Name = "Comprobante Externo de Pago")]
        public IFormFile? ArchivoComprobante { get; set; } 
    }
}