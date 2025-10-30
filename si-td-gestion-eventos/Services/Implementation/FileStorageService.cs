using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using si_td_gestion_eventos.Services.Contracts;
using System;
using System.IO;
using System.Threading.Tasks;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;

        public FileStorageService(IWebHostEnvironment env)
        {
            _env = env; // Inyecta IWebHostEnvironment para saber dónde está wwwroot
        }

        public async Task<string> GuardarArchivoAsync(IFormFile archivo, string carpetaDestino)
        {
            if (archivo == null || archivo.Length == 0)
                return null;

            var carpetaAbsoluta = Path.Combine(_env.WebRootPath, carpetaDestino);
            if (!Directory.Exists(carpetaAbsoluta))
            {
                Directory.CreateDirectory(carpetaAbsoluta);
            }

            var extension = Path.GetExtension(archivo.FileName);
            var nombreArchivo = $"{Guid.NewGuid()}{extension}";
            var rutaAbsoluta = Path.Combine(carpetaAbsoluta, nombreArchivo);

            try
            {
                await using (var stream = new FileStream(rutaAbsoluta, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                // Devuelve la ruta relativa para guardar en la DB
                return Path.Combine(carpetaDestino, nombreArchivo).Replace("\\", "/");
            }
            catch (Exception)
            {
                // Loggear error
                return null;
            }
        }

        public Task BorrarArchivoAsync(string rutaRelativa)
        {
            if (string.IsNullOrEmpty(rutaRelativa))
                return Task.CompletedTask;

            var rutaAbsoluta = Path.Combine(_env.WebRootPath, rutaRelativa);
            try
            {
                if (File.Exists(rutaAbsoluta))
                {
                    File.Delete(rutaAbsoluta);
                }
            }
            catch (Exception)
            {
                // Loggear error (ej. archivo no encontrado)
            }
            return Task.CompletedTask;
        }
    }
}