using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class OrderRepository : Repo<DatabaseContext, OrderEntity>
{
	public OrderRepository(IDbContextFactory<DatabaseContext> contextFactory)
		: base(contextFactory)
	{
	}

	public override async Task<IEnumerable<OrderEntity>> GetAllAsync()
	{
		using var context = CreateContext();
		try
		{
			return await context.Orders
				.Include(i => i.OrderDetails)
				.ThenInclude(d => d.Product)
				.ThenInclude(p => p.Brand)
				.Include(c => c.Courier)
				.Include(s => s.Storekeeper)
				.Include(c => c.Customer)
				.ToListAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting entities of type {typeof(OrderEntity).Name}: {ex.Message}");
			return Enumerable.Empty<OrderEntity>();
		}
	}

	public override async Task<OrderEntity?> GetOneAsync(
	Expression<Func<OrderEntity, bool>> predicate,
	Func<Task<OrderEntity>>? createIfNotFound = null)
	{
		using var context = CreateContext();
		var entity = await context.Orders
			.Include(o => o.OrderDetails)
				.ThenInclude(d => d.Product)
				.ThenInclude(p => p.Brand)
			.Include(o => o.Courier)
			.Include(c => c.Customer)
			.Include(o => o.Storekeeper)
			.FirstOrDefaultAsync(predicate);

		if (entity == null && createIfNotFound != null)
		{
			entity = await createIfNotFound();
			context.Set<OrderEntity>().Add(entity);
			await context.SaveChangesAsync();
		}

		return entity;
	}


	public async Task<bool> ExistsAsync(Expression<Func<OrderEntity, bool>> predicate, CancellationToken ct = default)
	{
		await using var context = CreateContext();
		return await context.Orders.AsNoTracking().AnyAsync(predicate, ct);
	}

}
