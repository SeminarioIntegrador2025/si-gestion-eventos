using System.ComponentModel.DataAnnotations.Schema;

namespace si_td_gestion_eventos.Entities
{
    public class CertificadoAGADU : ServicioEsencial
    {
        public string? RutaArchivo { get; set; }
        public DateTime? FechaAdjunto { get; set; }
        public bool Verificado { get; set; } = false; 
    }
}