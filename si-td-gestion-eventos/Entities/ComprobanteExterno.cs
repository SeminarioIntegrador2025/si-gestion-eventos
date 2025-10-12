namespace si_td_gestion_eventos.Entities
{
    public class ComprobanteExterno
    {
        public int Id { get; set; }
        public required string NombreArchivo { get; set; }
        public required string UrlArchivo { get; set; }
        public required DateTime FechaComprobante { get; set; } = DateTime.UtcNow;
        // relaciones con otros modelos
        public required int PagoId { get; set; }
        public required Pago Pago { get; set; } = null!;
    }
}
