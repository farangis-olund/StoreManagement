
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class OrderDetailRepository : Repo<DatabaseContext, OrderDetailEntity>
{
	public OrderDetailRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
	{
	}

	public async override Task<IEnumerable<OrderDetailEntity>> GetAllAsync()
	{
		using var context = CreateContext();
		try
		{
			List<OrderDetailEntity> orderList = await context.OrderDetails
			.Include(i => i.Product)
			.ToListAsync();

			return orderList;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting entities of type {typeof(OrderDetailEntity).Name}: {ex.Message}");
			return Enumerable.Empty<OrderDetailEntity>();
		}
	}

	public override async Task<OrderDetailEntity?> GetOneAsync(
	Expression<Func<OrderDetailEntity, bool>> predicate,
	Func<Task<OrderDetailEntity>>? createIfNotFound = null)
	{
		using var context = CreateContext();
		try
		{
			var entity = await context.OrderDetails
				.Include(d => d.Product)
				.ThenInclude(p => p.Brand)
				.FirstOrDefaultAsync(predicate);

			if (entity == null && createIfNotFound != null)
			{
				entity = await createIfNotFound.Invoke();
				context.Set<OrderDetailEntity>().Add(entity);
				await context.SaveChangesAsync();
			}

			return entity;
		}
		catch (Exception ex)
		{
			Debug.WriteLine(
				$"Error getting entity of type {typeof(OrderDetailEntity).Name} by predicate: {ex.Message}");
			return null!;
		}
	}

}
