
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public abstract class Repo<TContext, TEntity>
	where TContext : DbContext
	where TEntity : class
{
    protected readonly IDbContextFactory<TContext> _contextFactory;

	protected Repo(IDbContextFactory<TContext> contextFactory)
	{
		_contextFactory = contextFactory;
	}

	protected TContext CreateContext() => _contextFactory.CreateDbContext();

	public virtual async Task<TEntity> AddAsync(TEntity entity)
	{
		try
		{
			using var context = CreateContext();
			context.Set<TEntity>().Add(entity);
			await context.SaveChangesAsync();

			Debug.WriteLine($"Entity of type {typeof(TEntity).Name} added successfully");
			return entity;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error adding entity of type {typeof(TEntity).Name}: {ex.Message}");
			return Activator.CreateInstance<TEntity>()!;
		}
	}

	public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
	{
		try
		{
			using var context = CreateContext();
			return await context.Set<TEntity>().ToListAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting entities of type {typeof(TEntity).Name}: {ex.Message}");
			return Enumerable.Empty<TEntity>();
		}
	}

	public virtual async Task<TEntity> GetOneAsync(
		Expression<Func<TEntity, bool>> predicate,
		Func<Task<TEntity>>? createIfNotFound = null)
	{
		try
		{
			using var context = CreateContext();
			var entity = await context.Set<TEntity>().FirstOrDefaultAsync(predicate);

			if (entity == null && createIfNotFound != null)
			{
				entity = await createIfNotFound();
				context.Set<TEntity>().Add(entity);
				await context.SaveChangesAsync();
			}

			return entity ?? Activator.CreateInstance<TEntity>()!;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting entity of type {typeof(TEntity).Name}: {ex.Message}");
			return Activator.CreateInstance<TEntity>()!;
		}
	}

	public virtual async Task<TEntity> UpdateAsync(Expression<Func<TEntity, bool>> predicate, TEntity entity)
	{
		try
		{
			using var context = CreateContext();
			var updateEntity = await context.Set<TEntity>().FirstOrDefaultAsync(predicate);

			if (updateEntity == null)
				return Activator.CreateInstance<TEntity>()!;

			context.Entry(updateEntity).CurrentValues.SetValues(entity);
			await context.SaveChangesAsync();

			return entity;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error updating entity of type {typeof(TEntity).Name}: {ex.Message}");
			return Activator.CreateInstance<TEntity>()!;
		}
	}

	public virtual async Task<bool> RemoveAsync(Expression<Func<TEntity, bool>> predicate)
	{
		try
		{
			using var context = CreateContext();
			var entity = await context.Set<TEntity>().FirstOrDefaultAsync(predicate);
			if (entity == null) return false;

			context.Set<TEntity>().Remove(entity);
			await context.SaveChangesAsync();

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
			using var context = CreateContext();
			context.Set<TEntity>().Remove(entity);
			await context.SaveChangesAsync();
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
			using var context = CreateContext();
			return await context.Set<TEntity>().AnyAsync(predicate);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error checking existence of entity of type {typeof(TEntity).Name}: {ex.Message}");
			return false;
		}
	}
}
