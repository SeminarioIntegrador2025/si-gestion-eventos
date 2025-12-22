using AutoMapper;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;
using System.Linq.Expressions;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class FianzaService : IFianzaService
    {
        private readonly IGenericRepository<Fianza> _fianzaRepo;
        private readonly IGenericRepository<Evento> _eventoRepo;
        private readonly IMapper _mapper;
        private readonly AppDbContext _context;

        public FianzaService(
            IGenericRepository<Fianza> fianzaRepo,
            IGenericRepository<Evento> eventoRepo,
            IMapper mapper,
            AppDbContext context)
        {
            _fianzaRepo = fianzaRepo;
            _eventoRepo = eventoRepo;
            _mapper = mapper;
            _context = context;
        }

        // --- MÉTODO CREATE (Alineado con tu FianzaVM) ---
        public async Task<ServiceResult<FianzaVM>> CreateAsync(FianzaVM fianzaVM)
        {
            var evento = await _eventoRepo.GetByIdAsync(fianzaVM.EventoId);
            if (evento == null)
                return ServiceResult<FianzaVM>.FailureResult("El evento asociado no existe.");

            if (evento.FianzaId.HasValue)
                return ServiceResult<FianzaVM>.FailureResult("Este evento ya tiene una fianza registrada.");

            // --- CORRECCIÓN: Usamos los nombres reales de tu Entidad ---
            var fianza = new Fianza
            {
                EventoId = fianzaVM.EventoId,
                Monto = fianzaVM.Monto,                 // Antes decía MontoInicial
                FechaRegistro = fianzaVM.FechaRegistro, // Antes decía FechaAlta
                Observaciones = fianzaVM.Observaciones,
                Estado = EstadoFianza.Registrada
            };

            await using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _fianzaRepo.AddAsync(fianza);
                    await _fianzaRepo.SaveChangesAsync();

                    evento.FianzaId = fianza.FianzaId; // Antes decía Id
                    _eventoRepo.Update(evento);
                    await _eventoRepo.SaveChangesAsync();

                    await transaction.CommitAsync();

                    fianzaVM.FianzaId = fianza.FianzaId; // Antes decía Id
                    return ServiceResult<FianzaVM>.SuccessResult(fianzaVM, "Fianza registrada exitosamente.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<FianzaVM>.FailureResult($"Error: {ex.Message}");
                }
            }
        }

        // --- MÉTODO GETPAGINATED (Devuelve FianzaVM) ---
        public async Task<PaginatedList<FianzaVM>> GetPaginatedAsync(string? q, EstadoFianza? estado, int page, int pageSize)
        {
            // 1. Normalización de la búsqueda (Ingeniería de Software: Fail-fast)
            string queryLower = q?.Trim().ToLower() ?? "";
            // Limpiamos puntos y guiones para buscar solo los números en CI o RUT
            string queryNumbers = q?.Replace(".", "").Replace("-", "").Trim() ?? "";

            // 2. Predicado robusto con manejo de nulos y normalización
            Expression<Func<Fianza, bool>> predicate = f =>
                string.IsNullOrEmpty(queryLower) ||
                f.Evento.Cliente.Nombre.ToLower().Contains(queryLower) ||
                (f.Evento.Cliente.Apellido != null && f.Evento.Cliente.Apellido.ToLower().Contains(queryLower)) ||
                // Búsqueda por nombre completo
                (f.Evento.Cliente.Nombre + " " + (f.Evento.Cliente.Apellido ?? "")).ToLower().Contains(queryLower) ||
                // Búsqueda por CI (Normalizando los datos de la DB también)
                (f.Evento.Cliente.CedulaIdentidad != null &&
                 f.Evento.Cliente.CedulaIdentidad.Replace(".", "").Replace("-", "").Contains(queryNumbers)) ||
                // Búsqueda por RUT
                (f.Evento.Cliente.RUT != null &&
                 f.Evento.Cliente.RUT.Replace(".", "").Replace("-", "").Contains(queryNumbers)) ||
                // Búsqueda por Tipo de Evento (Enum a String)
                f.Evento.Tipo.ToString().ToLower().Contains(queryLower);

            var fianzasQuery = (await _fianzaRepo.FindWithIncludesAsync(
                                            predicate,
                                            f => f.Evento,
                                            f => f.Evento.Cliente
                                        )).AsQueryable();

            if (estado.HasValue)
            {
                fianzasQuery = fianzasQuery.Where(f => f.Estado == estado.Value);
            }

            var fianzasOrdenadas = fianzasQuery.OrderByDescending(f => f.FechaRegistro);
            var totalCount = fianzasOrdenadas.Count();
            var fianzasPaginadas = fianzasOrdenadas.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var items = _mapper.Map<List<FianzaVM>>(fianzasPaginadas);

            return new PaginatedList<FianzaVM>(items, totalCount, page, pageSize);
        }

        public async Task<FianzaVM?> GetByIdAsync(int id)
        {
            var fianza = await _fianzaRepo.GetByIdWithIncludesAsync(id, f => f.Evento, f => f.Evento.Cliente);
            if (fianza == null) return null;
            return _mapper.Map<FianzaVM>(fianza);
        }

        public async Task<ServiceResult<FianzaVM>> UpdateAsync(FianzaVM fianzaVM)
        {
            var fianza = await _fianzaRepo.GetByIdAsync(fianzaVM.FianzaId);
            if (fianza == null)
                return ServiceResult<FianzaVM>.FailureResult("Fianza no encontrada.");

            // Validar que el monto devuelto no exceda el monto original registrado
            if (fianzaVM.MontoDevuelto.HasValue && fianzaVM.MontoDevuelto > fianza.Monto)
                return ServiceResult<FianzaVM>.FailureResult($"El monto a devolver ({fianzaVM.MontoDevuelto:C2}) no puede ser mayor al monto de la fianza ({fianza.Monto:C2}).");

            // Preservamos las observaciones enviadas desde la vista
            fianza.Observaciones = fianzaVM.Observaciones;

            // --- NUEVA LÓGICA DE ESTADOS BASADA EN EL MONTO ---
            if (fianzaVM.MontoDevuelto.HasValue)
            {
                // Asignamos el monto y la fecha (si no viene fecha, usamos hoy)
                fianza.MontoDevuelto = fianzaVM.MontoDevuelto.Value;
                fianza.FechaDevolucion = fianzaVM.FechaDevolucion ?? DateTime.Today;

                if (fianza.MontoDevuelto > 0)
                {
                    // Caso A: Se devolvió dinero (Total o Parcial)
                    if (fianza.MontoDevuelto >= fianza.Monto)
                        fianza.Estado = EstadoFianza.DevueltaTotalmente;
                    else
                        fianza.Estado = EstadoFianza.DevueltaParcialmente;
                }
                else
                {
                    // Caso B: El usuario ingresó 0 (No se devuelve nada)
                    fianza.Estado = EstadoFianza.NoDevuelta;
      
                }
            }
            else
            {
                // Caso C: El monto es NULL (Se "resetea" a estado inicial)
                fianza.MontoDevuelto = null;
                fianza.FechaDevolucion = null;
                fianza.Estado = EstadoFianza.Registrada;
            }

            try
            {
                _fianzaRepo.Update(fianza);
                await _fianzaRepo.SaveChangesAsync();

                var updatedVM = await GetByIdAsync(fianza.FianzaId);
                return ServiceResult<FianzaVM>.SuccessResult(updatedVM!, "Fianza procesada correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<FianzaVM>.FailureResult($"Error al actualizar: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int id)
        {
            var fianza = await _fianzaRepo.GetByIdAsync(id);
            if (fianza == null)
                return ServiceResult<bool>.FailureResult("Fianza no encontrada.");

            var evento = await _eventoRepo.GetByIdAsync(fianza.EventoId);

 
            // Si el evento existe y ya se realizo no se puede borrar
            if (evento != null && evento.Estado == EventoEstado.Realizado)
            {
                return ServiceResult<bool>.FailureResult("No se puede eliminar la fianza porque el evento ya fue realizado. Si desea devolver el dinero, utilice la opción de Editar.");
            }

            await using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    //Desvincular del evento (Si existe)
                    if (evento != null)
                    {
                        evento.FianzaId = null;
                        _eventoRepo.Update(evento);
                        await _eventoRepo.SaveChangesAsync();
                    }

                    //Borrar la fianza físicamente
                    _fianzaRepo.Remove(fianza);
                    await _fianzaRepo.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return ServiceResult<bool>.SuccessResult(true, "La fianza ha sido eliminada y desvinculada del evento correctamente.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // Esto captura errores de SQL (ej: si hay Pagos vinculados que impiden borrar)
                    return ServiceResult<bool>.FailureResult($"Error al eliminar la fianza: {ex.Message}");
                }
            }
        }
    }
}