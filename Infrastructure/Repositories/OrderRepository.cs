
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class OrderRepository : Repo<DatabaseContext, OrderEntity>
{
	public OrderRepository(DatabaseContext context)
		: base(context)
	{

	}

	public async override Task<IEnumerable<OrderEntity>> GetAllAsync()
	{
		try
		{
			List<OrderEntity> orderList = await _context.Orders
			.Include(i => i.OrderDetails)
			.Include(c => c.Courier)
			.Include(s => s.Storekeeper)
			.ToListAsync();

			return orderList;
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
		var entity = await _context.Orders
			.Include(o => o.OrderDetails)
			.ThenInclude(d => d.Product)
			.Include(o => o.Courier)
			.Include(o => o.Storekeeper)
			.FirstOrDefaultAsync(predicate);

		if (entity == null && createIfNotFound != null)
		{
			entity = await createIfNotFound();
			_context.Set<OrderEntity>().Add(entity);
			await _context.SaveChangesAsync();
		}
		return entity;
	}

	public Task<bool> ExistsAsync(Expression<Func<OrderEntity, bool>> predicate, CancellationToken ct = default) =>
	_context.Orders.AsNoTracking().AnyAsync(predicate, ct);

}