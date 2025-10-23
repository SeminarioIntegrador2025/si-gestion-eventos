using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using System.Globalization; // Para CultureInfo

namespace si_td_gestion_eventos.Mapping
{
    public class ClienteProfile : Profile
    {
        public ClienteProfile()
        {
            // --- Mapeo de Cliente (Entidad) HACIA ClienteVM (ViewModel) ---
            CreateMap<Cliente, ClienteVM>()
                .ForMember(dest => dest.Nombre, opt => opt.MapFrom(src => src.Nombre != null ? src.Nombre.Trim() : null)) // Agrega chequeo
                                                                                                                          // ARREGLO 1: Comprobación de nulidad para Apellido
                .ForMember(dest => dest.Apellido, opt => opt.MapFrom(src => src.Apellido != null ? src.Apellido.Trim() : null))
                // ARREGLO 2: Comprobación de nulidad para CedulaIdentidad
                .ForMember(dest => dest.CedulaIdentidad, opt => opt.MapFrom(src => src.CedulaIdentidad != null ? src.CedulaIdentidad.Trim() : null))
                // ARREGLO 3: Agregado mapeo para RUT (con chequeo)
                .ForMember(dest => dest.RUT, opt => opt.MapFrom(src => src.RUT != null ? src.RUT.Trim() : null))
                .ForMember(dest => dest.Domicilio, opt => opt.MapFrom(src => src.Domicilio != null ? src.Domicilio.Trim() : null)) // Agrega chequeo
                .ForMember(dest => dest.Telefono, opt => opt.MapFrom(src => src.Telefono != null ? src.Telefono.Trim() : null)); // Agrega chequeo


            // --- Mapeo de ClienteVM (ViewModel) HACIA Cliente (Entidad) ---
            CreateMap<ClienteVM, Cliente>()
                // ARREGLO 4: Comprobación de nulidad y manejo ToTitleCase
                .ForMember(dest => dest.Nombre, opt => opt.MapFrom(src => src.Nombre.ToTitleCase())) // Asume que Nombre nunca es null
                .ForMember(dest => dest.Apellido, opt => opt.MapFrom(src => src.Apellido.ToTitleCase())) // Usa la extensión segura
                .ForMember(dest => dest.CedulaIdentidad, opt => opt.MapFrom(src => src.CedulaIdentidad != null ? src.CedulaIdentidad.Trim() : null)) // Quita ToTitleCase si no aplica
                                                                                                                                                     // ARREGLO 5: Agregado mapeo para RUT
                .ForMember(dest => dest.RUT, opt => opt.MapFrom(src => src.RUT != null ? src.RUT.Trim() : null))
                .ForMember(dest => dest.Domicilio, opt => opt.MapFrom(src => src.Domicilio != null ? src.Domicilio.Trim() : null)) // Agrega chequeo
                .ForMember(dest => dest.Telefono, opt => opt.MapFrom(src => src.Telefono != null ? src.Telefono.Trim() : null)) // Agrega chequeo
                                                                                                                                // ARREGLO 6: Agregado mapeo para Tipo
                .ForMember(dest => dest.Tipo, opt => opt.MapFrom(src => src.Tipo))
                .ForMember(dest => dest.Eventos, opt => opt.Ignore()); // Correcto
        }
    }

    // Extensión ToTitleCase segura (maneja null)
    public static class StringExtensions
    {
        public static string? ToTitleCase(this string? input) // Acepta y devuelve nullable
        {
            if (string.IsNullOrWhiteSpace(input)) return input; // Devuelve null/vacío si entra así

            // Usa InvariantCulture para evitar problemas con configuraciones regionales
            // y ToLowerInvariant() para consistencia.
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.Trim().ToLowerInvariant());
        }
    }
}