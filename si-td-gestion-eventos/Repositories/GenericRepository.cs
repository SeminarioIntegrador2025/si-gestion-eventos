using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using System.Linq.Expressions;

namespace si_td_gestion_eventos.Repositories
{
    public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
    {
        private readonly AppDbContext _dbContext;
        private readonly DbSet<TEntity> _dbSet;

        public GenericRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = _dbContext.Set<TEntity>();
        }

        public async Task<IEnumerable<TEntity>> GetAllAsync()
            => await _dbSet.AsNoTracking().ToListAsync();

        public async Task<TEntity?> GetByIdAsync(int id)
            => await _dbSet.FindAsync(id);

        public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
            => await _dbSet.AsNoTracking().Where(predicate).ToListAsync();

        public async Task<IEnumerable<TEntity>> FindWithIncludesAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = _dbSet.AsNoTracking();

            // Aplicar includes
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            // Aplicar filtro si existe
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            return await query.ToListAsync();
        }

        public async Task<TEntity?> GetByIdWithIncludesAsync(int id, params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = _dbSet.AsQueryable();

            // Aplicar includes
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            // Buscar por la propiedad correcta según el tipo de entidad
            var entityType = typeof(TEntity);
            var primaryKeyName = $"{entityType.Name}Id"; // Para Evento sería "EventoId"

            // Usar reflexión para encontrar la propiedad de ID correcta
            var idProperty = entityType.GetProperty(primaryKeyName) ?? entityType.GetProperty("Id");
            
            if (idProperty == null)
                throw new InvalidOperationException($"No se encontró una propiedad de ID válida en {entityType.Name}");

            return await query.FirstOrDefaultAsync(e => EF.Property<int>(e, idProperty.Name) == id);
        }

        public async Task AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public void Update(TEntity entity)
        {
            _dbSet.Update(entity);
        }

        public void Remove(TEntity entity)
        {
            _dbSet.Remove(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}