
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class Repo
{
}
public abstract class Repo<TContext, TEntity> 
    where TContext : DbContext where TEntity : class
{
    protected readonly TContext _context;

    protected Repo(TContext context)
    {
        _context = context;

    }

    public virtual async Task<TEntity> AddAsync(TEntity entity)
    {
        try
        {
            _context.Set<TEntity>().Add(entity);
            await _context.SaveChangesAsync();

            Debug.WriteLine($"Entity of type {typeof(TEntity).Name} added successfully: {entity}");
            return entity;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding entity of type {typeof(TEntity).Name}: {ex.Message}");
            return null!;
        }

    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        try
        {
            return await _context.Set<TEntity>().ToListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting entities of type {typeof(TEntity).Name}: {ex.Message}");
            return Enumerable.Empty<TEntity>();
        }
    }

    public virtual async Task<TEntity> GetOneAsync(Expression<Func<TEntity, bool>> predicate, Func<Task<TEntity>> createIfNotFound)
    {
        try
        {
            var entity = await _context.Set<TEntity>().FirstOrDefaultAsync(predicate);

            if (entity == null)
            {
                entity = await createIfNotFound.Invoke();
                _context.Set<TEntity>().Add(entity);
            }

            return entity;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting entity of type {typeof(TEntity).Name} by id: {ex.Message}");
            return null!;
        }
    }

    public virtual async Task<TEntity> GetOneAsync(Expression<Func<TEntity, bool>> predicate)
    {
        try
        {
            var entity = await _context.Set<TEntity>().FirstOrDefaultAsync(predicate);
            if (entity != null)
            {
                return entity;
            }
            return null!;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting entity of type {typeof(TEntity).Name} by id: {ex.Message}");
            return null!;
        }
    }

    public virtual async Task<TEntity> UpdateAsync(Expression<Func<TEntity, bool>> predicate, TEntity entity)
    {
        try
        {

            var updateEntity = _context.Set<TEntity>().FirstOrDefault(predicate);
            _context.Entry(updateEntity!).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating entity of type {typeof(TEntity).Name}: {ex.Message}");
            return null!;
        }
    }

    public virtual async Task<bool> RemoveAsync(Expression<Func<TEntity, bool>> predicate)
    {
        try
        {
            var entity = await _context.Set<TEntity>().FirstOrDefaultAsync(predicate);
            if (entity == null)
            {
                return false;
            }

            _context.Set<TEntity>().Remove(entity);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing entity of type {typeof(TEntity).Name}: {ex.Message}");
            return false;
        }
    }

    public virtual async Task<bool> RemoveAsync(TEntity entity)
    {
        try
        {
            _context.Set<TEntity>().Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing entity of type {typeof(TEntity).Name}: {ex.Message}");
            return false;
        }
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate)
    {
        try
        {
            return await _context.Set<TEntity>().AnyAsync(predicate);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking existence of entity of type {typeof(TEntity).Name}: {ex.Message}");
            return false;
        }
    }



}
