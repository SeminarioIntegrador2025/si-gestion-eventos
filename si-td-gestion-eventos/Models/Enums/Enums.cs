using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Models.Enums
{
    public enum TipoCliente
    {
       PersonaFisica = 1,
       PersonaJuridica = 2
    }
    public enum EventoEstado
    {
        [Display(Name = "Confirmado")]
        Confirmado,
        [Display(Name = "Cancelado")]
        Cancelado,
        [Display(Name = "Reprogramado")]
        Reprogramado,
    }

    public enum MetodoPago
    {
        [Display(Name = "Efectivo")]
        Efectivo,
        [Display(Name = "Transferencia Bancaria")]
        Transferencia
    }

    public enum EstadoFianza
    {
        [Display(Name = "Devuelta totalmente")]
        DevueltaTotalmente,
        [Display(Name = "Devuelta parcialmente")]
        DevueltaParcialmente,
        [Display(Name = "No devuelta")]
        NoDevuelta,
        [Display(Name = "Registrada")]
        Registrada
    }

    public enum TipoArchivo
    {
        [Display(Name = "PDF")]
        PDF,
        [Display(Name = "Imagen PNG")]
        PNG,
        [Display(Name = "Imagen JPEG")]
        JPEG,
        [Display(Name = "Imagen JPG")]
        JPG
    }

    public enum TipoEvento
    {
        [Display(Name = "Cumpleaños")]
        Cumpleaños,
        [Display(Name = "Casamiento")]
        Casamiento,
        [Display(Name = "Corporativo")]
        Corporativo,
        [Display(Name = "Otro")]
        Otro
    }
}
