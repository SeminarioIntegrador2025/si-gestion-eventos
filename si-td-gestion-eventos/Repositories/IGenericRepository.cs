using System.Linq.Expressions;

namespace si_td_gestion_eventos.Repositories
{
    public interface IGenericRepository<TEntity> where TEntity : class
    {
        Task<IEnumerable<TEntity>> GetAllAsync();

        Task<TEntity?> GetByIdAsync(int id);

        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

        Task AddAsync(TEntity entity);

        // Se renombra a 'Update' y se hace síncrono
        void Update(TEntity entity);

        // Se renombra a 'Remove' y se hace síncrono
        void Remove(TEntity entity);

        Task SaveChangesAsync();
    }
}