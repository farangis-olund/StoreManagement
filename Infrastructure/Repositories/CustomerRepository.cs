using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class CustomerRepository : Repo<DatabaseContext, CustomerEntity>
{
	public CustomerRepository(IDbContextFactory<DatabaseContext> contextFactory)
		: base(contextFactory)
	{
	}

	public override async Task<IEnumerable<CustomerEntity>> GetAllAsync()
	{
		try
		{
			using var context = CreateContext();

			var customers = await context.Customers
				.Include(i => i.PriceLevel)
				.Include(c => c.Payments)
				.ToListAsync();

			return customers;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting entities of type {typeof(CustomerEntity).Name}: {ex.Message}");
			return Enumerable.Empty<CustomerEntity>();
		}
	}

	public override async Task<CustomerEntity?> GetOneAsync(
		Expression<Func<CustomerEntity, bool>> predicate,
		Func<Task<CustomerEntity>>? createIfNotFound = null)
	{
		try
		{
			using var context = CreateContext();

			var entity = await context.Customers
				.Include(i => i.PriceLevel)
				.Include(c => c.Payments)
				.FirstOrDefaultAsync(predicate);

			if (entity == null && createIfNotFound != null)
			{
				entity = await createIfNotFound.Invoke();
				context.Set<CustomerEntity>().Add(entity);
				await context.SaveChangesAsync();
			}

			return entity;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting entity of type {typeof(CustomerEntity).Name} by predicate: {ex.Message}");
			return null;
		}
	}
}
