namespace si_td_gestion_eventos.Models.Enums
{
    public enum EventoEstado { Confirmado, Cancelado, Reprogramado, PendienteAConfirmar }
    public enum MetodoPago { Efectivo, Transferencia }
    public enum EstadoFianza { DevueltaTotalmente, DevueltaParcialmente, NoDevuelta, Registrada }
    public enum EstadoServicioEsencial { SinConfirmar, Confirmado }
    public enum TipoArchivo { PDF, PNG, JPEG, JPG }
    public enum TipoEvento { Cumpleanios, Casamiento, Corporativo, Otro }
}
