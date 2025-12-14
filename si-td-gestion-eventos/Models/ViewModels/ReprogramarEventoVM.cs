using System;
using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class ReprogramarEventoVM
    {
        public int EventoId { get; set; }

        // Nuevo campo para indicar que no hay fecha aún
        public bool FechaIndefinida { get; set; }

        // Ahora son anulables (DateTime?)
        [DataType(DataType.Date)]
        public DateTime? NuevaFecha { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? NuevaHoraInicio { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? NuevaHoraFin { get; set; }
    }
}