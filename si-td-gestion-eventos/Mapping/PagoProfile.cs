// En Mapping/PagoProfile.cs
using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.Mapping
{
    public class PagoProfile : Profile
    {
        public PagoProfile()
        {

            CreateMap<Pago, PagoVM>()

                .ForMember(dest => dest.RutaArchivoExistente, opt => opt.MapFrom(src => src.ComprobanteExterno != null ? src.ComprobanteExterno.RutaArchivo : null))

                .ForMember(dest => dest.TipoArchivoComprobante, opt => opt.MapFrom(src => src.ComprobanteExterno != null ? (TipoArchivo?)src.ComprobanteExterno.TipoArchivo : null));

          
            CreateMap<PagoVM, Pago>()
               
                .ForMember(dest => dest.Evento, opt => opt.Ignore())
                .ForMember(dest => dest.ComprobanteExterno, opt => opt.Ignore()) 
                .ForMember(dest => dest.PagoId, opt => opt.Ignore()); 

         
            CreateMap<PagoVM, ComprobanteExterno>()
                .ForMember(dest => dest.FechaComprobante, opt => opt.MapFrom(src => DateTime.Now)) 
                .ForMember(dest => dest.TipoArchivo, opt => opt.MapFrom(src => src.TipoArchivoComprobante ?? TipoArchivo.PDF))
                .ForMember(dest => dest.RutaArchivo, opt => opt.Ignore()) 
                .ForMember(dest => dest.Pago, opt => opt.Ignore()) 
                .ForMember(dest => dest.PagoId, opt => opt.Ignore())
                .ForMember(dest => dest.ComprobanteExternoId, opt => opt.Ignore());
        }
    }
}