// En Models/ViewModels/FianzaVM.cs
using si_td_gestion_eventos.Models.Enums; // Para EstadoEstadoFianza
using System; // Para DateTime
using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class FianzaVM
    {
        public int FianzaId { get; set; }

        [Display(Name = "Monto Inicial")]
        public float MontoInicial { get; set; } // O decimal si preferís

        [Display(Name = "Monto Devuelto")]
        public float? MontoDevuelto { get; set; } // Puede ser nulo si no se devolvió

        [Display(Name = "Fecha Acta")]
        public DateTime FechaActa { get; set; }

        [Display(Name = "Fecha Devolución")]
        public DateTime? FechaDevolucion { get; set; } // Puede ser nulo

        [Display(Name = "Estado Fianza")]
        public EstadoFianza Estado { get; set; } // Usa tu enum
    }
}