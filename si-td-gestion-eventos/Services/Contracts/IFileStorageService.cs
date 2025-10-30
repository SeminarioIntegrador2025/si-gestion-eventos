using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IFileStorageService
    {
        Task<string> GuardarArchivoAsync(IFormFile archivo, string carpetaDestino);
        Task BorrarArchivoAsync(string rutaRelativa);
    }
}