using Microsoft.EntityFrameworkCore;
using System; // Para DateTime

namespace si_td_gestion_eventos.Entities
{
    [Owned] // Adueñado por ServiciosEsenciales
    public class CertificadoAGADU
    {
        public string? RutaArchivo { get; set; } // Puede ser null si no se adjunta
        public DateTime? FechaAdjunto { get; set; } // Puede ser null
        public bool Verificado { get; set; } = false; // Valor por defecto
    }
}