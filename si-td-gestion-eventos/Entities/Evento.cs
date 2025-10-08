using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Entities
{
    public class Evento
    {
        public int EventoId { get; set; }
        public required DateTime FechaContrato { get; set; }
        public required DateTime Inicio { get; set; }
        public required DateTime Fin { get; set; }
        public required TimeSpan HoraInicio { get; set; }
        public required TimeSpan HoraFin { get; set; }
                
        public required TipoEvento Tipo { get; set; }
        public required decimal CostoAlquiler { get; set; }
        public required decimal MontoReserva { get; set; }
        public required int CantidadPersonas { get; set; }
        public decimal? MontoAireAcondicionado { get; set; }
                
        public required string ResponsableNombre { get; set; } = null!;
        public required string ResponsableTelefono { get; set; } = null!;
        public required string ResponsableCedula { get; set; } = null!;
                
        public required EventoEstado Estado { get; set; } = EventoEstado.PendienteAConfirmar;

        // relaciones con otros modelos
        public required int ClienteId { get; set; }
        public required Cliente Cliente { get; set; } = null!;
                
        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
        public Fianza? Fianza { get; set; }

        public decimal TotalPagado() => Pagos.Sum(p => p.Monto);
        public decimal SaldoRestante() => Math.Max(0, CostoAlquiler - TotalPagado());

        public bool EstaPago48hAntes(DateTime ahoraUtc)
            => Inicio.ToUniversalTime() - ahoraUtc >= TimeSpan.FromHours(48) ? SaldoRestante() == 0 : true;
    }
}
