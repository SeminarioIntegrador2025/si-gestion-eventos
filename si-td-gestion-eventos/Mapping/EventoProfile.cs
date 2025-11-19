using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using System.Linq;

namespace si_td_gestion_eventos.Mapping
{
    public class EventoProfile : Profile
    {
        public EventoProfile()
        {
            // --- Mapeo de Evento (Entidad) HACIA EventoVM (ViewModel) ---
            CreateMap<Evento, EventoVM>()
                // Mapeo de Cliente
                .ForMember(dest => dest.ClienteNombreCompleto,
                           opt => opt.MapFrom(src => src.Cliente != null
                               ? $"{src.Cliente.Nombre} {src.Cliente.Apellido}"
                               : string.Empty))

                // Mapeo de Responsable (Owned Type)
                .ForMember(dest => dest.ResponsableNombre, opt => opt.MapFrom(src => src.ResponsableSalon.Nombre))
                .ForMember(dest => dest.ResponsableTelefono, opt => opt.MapFrom(src => src.ResponsableSalon.Telefono))
                .ForMember(dest => dest.ResponsableCedula, opt => opt.MapFrom(src => src.ResponsableSalon.CI))

                // Mapeo de Pagos (Totales)
                .ForMember(dest => dest.TotalPagado, opt => opt.MapFrom(src =>
                    src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0f))

                .ForMember(dest => dest.SaldoRestante, opt => opt.MapFrom(src =>
                    (src.CostoAlquiler + (src.MontoAireAcondicionado ?? 0f)) -
                    (src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0f)))

                .ForMember(dest => dest.TipoCli, opt => opt.MapFrom(src => src.Cliente.Tipo))

  

                .ForMember(dest => dest.FianzaId, opt => opt.MapFrom(src => src.FianzaId))

                .ForMember(dest => dest.DetalleFianza, opt => opt.MapFrom(src => src.Fianza));


            // --- Mapeo de EventoVM (ViewModel) HACIA Evento (Entidad) ---
            CreateMap<EventoVM, Evento>()
                .ForMember(dest => dest.Cliente, opt => opt.Ignore())
                .ForMember(dest => dest.Pagos, opt => opt.Ignore())
                .ForMember(dest => dest.Fianza, opt => opt.Ignore())
                .ForMember(dest => dest.Reportes, opt => opt.Ignore())
                // Mapeo inverso de Responsable
                .ForPath(dest => dest.ResponsableSalon.Nombre, opt => opt.MapFrom(src => src.ResponsableNombre))
                .ForPath(dest => dest.ResponsableSalon.Telefono, opt => opt.MapFrom(src => src.ResponsableTelefono))
                .ForPath(dest => dest.ResponsableSalon.CI, opt => opt.MapFrom(src => src.ResponsableCedula))

                .AfterMap((src, dest) => {
                    if (dest.ResponsableSalon == null)
                    {
                        dest.ResponsableSalon = new ResponsableSalon
                        {
                            Nombre = src.ResponsableNombre,
                            Telefono = src.ResponsableTelefono,
                            CI = src.ResponsableCedula
                        };
                    }
                    if (dest.ServiciosEsenciales == null)
                    {
                        dest.ServiciosEsenciales = new ServiciosEsenciales
                        {
                            CertificadoAGADU = new CertificadoAGADU()
                        };
                    }
                });
        }
    }
}