using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Models
{
    public class Evento
    {
        public int Id { get; set; }
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

        public string ResponsableNombre { get; set; } = null!;
        public string ResponsableTelefono { get; set; } = null!;
        public string ResponsableCedula { get; set; } = null!;

        public EventoEstado Estado { get; set; } = EventoEstado.PendienteAConfirmar;

        // relación con Cliente
        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; } = null!;

        // navegación
        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
        public Fianza? Fianza { get; set; }

        public decimal TotalPagado() => Pagos.Sum(p => p.Monto);
        public decimal SaldoRestante() => Math.Max(0, CostoAlquiler - TotalPagado());

        public bool EstaPago48hAntes(DateTime ahoraUtc)
            => (Inicio.ToUniversalTime() - ahoraUtc) >= TimeSpan.FromHours(48) ? SaldoRestante() == 0 : true;
    }
}
