
 using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Entities
{
    public class ComprobanteExterno
    {
        public int ComprobanteExternoId { get; set; }
        public required string NombreArchivo { get; set; }
        public required string RutaArchivo { get; set; }
        public required DateTime FechaComprobante { get; set; } = DateTime.UtcNow;

        public TipoArchivo TipoArchivo { get; set; }
        public string? Referencia { get; set; }
        
        // relaciones con otros modelos
        public required int PagoId { get; set; }
        public required Pago Pago { get; set; } = null!;
    }
}
