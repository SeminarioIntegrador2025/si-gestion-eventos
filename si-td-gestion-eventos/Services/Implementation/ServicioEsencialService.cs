using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class ServicioEsencialService : IServicioEsencialService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _env; // Para saber dónde guardar archivos

        public ServicioEsencialService(AppDbContext context, IMapper mapper, IWebHostEnvironment env)
        {
            _context = context;
            _mapper = mapper;
            _env = env;
        }

        public async Task<List<ServicioEsencialVM>> GetByEventoIdAsync(int eventoId)
        {
            // 1. Buscamos los servicios existentes
            var servicios = await _context.ServiciosEsenciales
                .Where(s => s.EventoId == eventoId)
                .ToListAsync();

            // 2. Si no hay ninguno, los inicializamos (Regla de Negocio RF-21)
            if (!servicios.Any())
            {
                await InicializarServiciosAsync(eventoId);
                servicios = await _context.ServiciosEsenciales
                    .Where(s => s.EventoId == eventoId)
                    .ToListAsync();
            }

            // Mapeo manual rápido (o puedes usar AutoMapper si configuras el Profile)
            var listaVM = new List<ServicioEsencialVM>();
            foreach (var s in servicios)
            {
                var vm = new ServicioEsencialVM
                {
                    Id = s.Id,
                    EventoId = s.EventoId,
                    Verificado = false, // Valor base
                    TipoServicio = "Desconocido"
                };

                // Casteamos para obtener datos específicos de AGADU
                if (s is CertificadoAGADU agadu)
                {
                    vm.TipoServicio = "AGADU";
                    vm.RutaArchivo = agadu.RutaArchivo;
                    vm.FechaAdjunto = agadu.FechaAdjunto;
                    vm.Verificado = agadu.Verificado;
                }

                listaVM.Add(vm);
            }

            return listaVM;
        }

        public async Task<ServiceResult<bool>> InicializarServiciosAsync(int eventoId)
        {
            try
            {
                // Por defecto creamos un AGADU vacío para este evento
                var agadu = new CertificadoAGADU
                {
                    EventoId = eventoId,
                    Verificado = false,
                    RutaArchivo = null
                };

                _context.ServiciosEsenciales.Add(agadu);
                await _context.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Error al inicializar servicios: {ex.Message}");
            }
        }

        public async Task<ServiceResult<ServicioEsencialVM>> SubirComprobanteAsync(int id, ServicioEsencialVM modelo)
        {
            var servicio = await _context.ServiciosEsenciales.FindAsync(id);
            if (servicio == null) return ServiceResult<ServicioEsencialVM>.FailureResult("Servicio no encontrado.");

            if (modelo.ArchivoSubido == null || modelo.ArchivoSubido.Length == 0)
                return ServiceResult<ServicioEsencialVM>.FailureResult("No se ha seleccionado ningún archivo válido.");

            // Validar extensión (RF-201)
            var extension = Path.GetExtension(modelo.ArchivoSubido.FileName).ToLower();
            string[] permitidos = { ".pdf", ".jpg", ".jpeg", ".png" };
            if (!permitidos.Contains(extension))
                return ServiceResult<ServicioEsencialVM>.FailureResult("Formato no válido. Use PDF, JPG o PNG.");

            try
            {
                // 1. Guardar archivo en disco (Carpeta wwwroot/uploads/comprobantes)
                string carpeta = Path.Combine(_env.WebRootPath, "uploads", "comprobantes");
                if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);

                string nombreArchivo = $"Evento_{servicio.EventoId}_AGADU_{Guid.NewGuid()}{extension}";
                string rutaCompleta = Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await modelo.ArchivoSubido.CopyToAsync(stream);
                }

                // 2. Actualizar Entidad (Casting a AGADU)
                if (servicio is CertificadoAGADU agadu)
                {
                    agadu.RutaArchivo = nombreArchivo;
                    agadu.FechaAdjunto = DateTime.Now;
                    agadu.Verificado = true; // Asumimos verificado al subir (RF-22)
                }

                _context.Update(servicio);
                await _context.SaveChangesAsync();

                return ServiceResult<ServicioEsencialVM>.SuccessResult(modelo, "Comprobante subido exitosamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<ServicioEsencialVM>.FailureResult($"Error al guardar archivo: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> VerificarServicioAsync(int id)
        {
            var servicio = await _context.ServiciosEsenciales.FindAsync(id);
            if (servicio is CertificadoAGADU agadu)
            {
                if (string.IsNullOrEmpty(agadu.RutaArchivo))
                    return ServiceResult<bool>.FailureResult("No se puede verificar si no hay comprobante adjunto.");

                agadu.Verificado = true;
                await _context.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true, "Servicio verificado correctamente.");
            }
            return ServiceResult<bool>.FailureResult("Tipo de servicio no válido.");
        }

        public async Task<ServiceResult<bool>> EliminarComprobanteAsync(int id)
        {
            var servicio = await _context.ServiciosEsenciales.FindAsync(id);
            if (servicio is CertificadoAGADU agadu)
            {
                //Borrar archivo
                agadu.RutaArchivo = null;
                agadu.FechaAdjunto = null;
                agadu.Verificado = false;

                await _context.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true, "Comprobante eliminado.");
            }
            return ServiceResult<bool>.FailureResult("No encontrado.");
        }
    }
}