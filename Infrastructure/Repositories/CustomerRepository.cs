using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories
{
	public class CustomerRepository : Repo<DatabaseContext, CustomerEntity>
	{
		public CustomerRepository(DatabaseContext context) : base(context)
		{
		}

		public async override Task<IEnumerable<CustomerEntity>> GetAllAsync()
		{
			try
			{
				List<CustomerEntity> productPriceList = await _context.Customers
				.Include(i => i.PriceLevel)
				.Include(c => c.Payments)
				.ToListAsync();

				return productPriceList;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error getting entities of type {typeof(CustomerEntity).Name}: {ex.Message}");
				return Enumerable.Empty<CustomerEntity>();
			}
		}

		public async override Task<CustomerEntity> GetOneAsync(Expression<Func<CustomerEntity, bool>> predicate, Func<Task<CustomerEntity>> createIfNotFound)
		{
			try
			{
				var entity = await _context.Customers
					 .Include(i => i.PriceLevel)
					 .FirstOrDefaultAsync(predicate);

				entity = await createIfNotFound.Invoke();
				_context.Set<CustomerEntity>().Add(entity);
				return entity;

			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error getting entity of type {typeof(CustomerEntity).Name} by id: {ex.Message}");
				return null!;
			}
		}
	}
}
