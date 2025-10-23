namespace si_td_gestion_eventos.Entities
{
    public class Reporte
    {
        public int ReporteId { get; set; }
        public float MontoAlquiler { get; set; }
        public float MontoPagado { get; set; }
        // ... otras propiedades ...

        // Relación
        public int EventoId { get; set; }
        public Evento Evento { get; set; }
    }
}