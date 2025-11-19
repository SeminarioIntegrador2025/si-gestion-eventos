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
            Expression<Func<Fianza, bool>> predicate = f =>
                string.IsNullOrEmpty(q) ||
                (f.Evento.Cliente.Nombre + " " + f.Evento.Cliente.Apellido).Contains(q) ||
                f.Evento.Cliente.CedulaIdentidad.Contains(q) ||
                f.Evento.Cliente.RUT.Contains(q) ||
                f.Evento.Tipo.ToString().Contains(q);

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
    }
}