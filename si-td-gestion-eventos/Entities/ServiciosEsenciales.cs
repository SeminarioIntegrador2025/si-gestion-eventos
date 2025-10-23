using Microsoft.EntityFrameworkCore;

namespace si_td_gestion_eventos.Entities
{
    [Owned]
    public class ServiciosEsenciales
    {
        public CertificadoAGADU CertificadoAGADU { get; set; }
    }
}