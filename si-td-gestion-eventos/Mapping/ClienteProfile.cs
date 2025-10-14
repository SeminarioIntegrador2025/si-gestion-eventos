using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.Mapping
{
    public class ClienteProfile : Profile
    {
        public ClienteProfile()
        {
            CreateMap<Cliente, ClienteVM>()
                .ForMember(dest => dest.Nombre, opt => opt.MapFrom(src => src.Nombre.Trim()))
                .ForMember(dest => dest.Apellido, opt => opt.MapFrom(src => src.Apellido.Trim()))
                .ForMember(dest => dest.CedulaIdentidad, opt => opt.MapFrom(src => src.CedulaIdentidad.Trim()))
                .ForMember(dest => dest.Domicilio, opt => opt.MapFrom(src => src.Domicilio.Trim()))
                .ForMember(dest => dest.Telefono, opt => opt.MapFrom(src => src.Telefono.Trim()));

            CreateMap<ClienteVM, Cliente>()
                .ForMember(dest => dest.Nombre, opt => opt.MapFrom(src => src.Nombre.Trim().ToTitleCase()))
                .ForMember(dest => dest.Apellido, opt => opt.MapFrom(src => src.Apellido.Trim().ToTitleCase()))
                .ForMember(dest => dest.CedulaIdentidad, opt => opt.MapFrom(src => src.CedulaIdentidad.Trim()))
                .ForMember(dest => dest.Domicilio, opt => opt.MapFrom(src => src.Domicilio.Trim()))
                .ForMember(dest => dest.Telefono, opt => opt.MapFrom(src => src.Telefono.Trim()))
                .ForMember(dest => dest.Eventos, opt => opt.Ignore()); // Ignorar propiedades de navegación
        }
    }

    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var trimmed = input.Trim();
            if (string.IsNullOrEmpty(trimmed)) return string.Empty;            
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(trimmed.ToLower());
        }
    }
}