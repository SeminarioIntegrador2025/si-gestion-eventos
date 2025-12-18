using System.ComponentModel.DataAnnotations;

public class ReprogramarEventoVM
{
    public int EventoId { get; set; }
    public bool FechaIndefinida { get; set; }

    [DataType(DataType.Date)]
    public DateTime? NuevaFechaInicio { get; set; } 

    [DataType(DataType.Time)]
    public TimeSpan? NuevaHoraInicio { get; set; }

    [DataType(DataType.Date)]
    public DateTime? NuevaFechaFin { get; set; } 

    [DataType(DataType.Time)]
    public TimeSpan? NuevaHoraFin { get; set; }
}