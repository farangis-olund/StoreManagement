
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
	public class ManagerService
	{
		private readonly DatabaseContext _context;

		public ManagerService(DatabaseContext context)
		{
			_context = context;
		}

		public async Task<List<SalesManagerEntity>> GetManagersAsync()
		{
			return await _context.SalesManagers
				.AsNoTracking()
				.OrderBy(x => x.Id)
				.ToListAsync();
		}

		public async Task<SalesManagerEntity?> GetManagerByIdAsync(string id)
		{
			return await _context.SalesManagers
				.Include(m => m.ManagerCustomers)
				.Include(m => m.ManagerBrands)
				.ThenInclude(mb => mb.Brand)
				.FirstOrDefaultAsync(m => m.Id == id);
		}

		public async Task<List<ManagerBrandEntity>> GetManagerBrandsAsync(string managerId)
		{
			return await _context.ManagerBrands
				.Include(x => x.Brand)
				.Where(x => x.ManagerId == managerId)
				.ToListAsync();
		}

		public async Task AddAllBrandsAsync(string managerId, double salePercent)
		{
			var brands = await _context.Brands.AsNoTracking().ToListAsync();

			var existing = await _context.ManagerBrands
				.Where(x => x.ManagerId == managerId)
				.ToListAsync();

			// Remove duplicates if already exist
			var newBrands = brands
				.Where(b => existing.All(x => x.BrandId != b.Id))
				.Select(b => new ManagerBrandEntity
				{
					ManagerId = managerId,
					BrandId = b.Id,
					SalesPercentage = salePercent
				}).ToList();

			_context.ManagerBrands.AddRange(newBrands);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAllBrandsAsync(string managerId)
		{
			var items = _context.ManagerBrands.Where(x => x.ManagerId == managerId);
			_context.ManagerBrands.RemoveRange(items);
			await _context.SaveChangesAsync();
		}

		// Save manager-customer relationships
		public async Task SaveManagerCustomersAsync(string managerId, IEnumerable<ManagerCustomerEntity> customers)
		{
			// Remove old links
			var existing = _context.ManagerCustomers
				.Where(x => x.ManagerId == managerId);
			_context.ManagerCustomers.RemoveRange(existing);

			// Add new links
			await _context.ManagerCustomers.AddRangeAsync(customers);

			// Save changes
			await _context.SaveChangesAsync();
		}

		// (optional sync version if you don't want async)
		public void SaveManagerCustomers(string managerId, IEnumerable<ManagerCustomerEntity> customers)
		{
			var existing = _context.ManagerCustomers
				.Where(x => x.ManagerId == managerId);
			_context.ManagerCustomers.RemoveRange(existing);
			_context.ManagerCustomers.AddRange(customers);
			_context.SaveChanges();
		}

		public async Task SaveManagerBrandsAsync(string managerId, IEnumerable<ManagerBrandEntity> brands)
		{
			var existing = _context.ManagerBrands.Where(x => x.ManagerId == managerId);
			_context.ManagerBrands.RemoveRange(existing);

			// Detach tracked ManagerBrandEntity objects
			var tracked = _context.ChangeTracker.Entries<ManagerBrandEntity>().ToList();
			foreach (var entry in tracked)
				entry.State = EntityState.Detached;

			// Add new fresh copies
			var newEntities = brands.Select(b => new ManagerBrandEntity
			{
				ManagerId = b.ManagerId,
				BrandId = b.BrandId,
				SalesPercentage = b.SalesPercentage
			});

			await _context.ManagerBrands.AddRangeAsync(newEntities);
			await _context.SaveChangesAsync();
		}

		public async Task<List<CustomerEntity>> GetUnassignedCustomersAsync()
		{
			// Get all customers
			var allCustomers = await _context.Customers.ToListAsync();

			// Get all customers that are already assigned to ANY manager
			var assignedCustomerIds = await _context.ManagerCustomers
				.Select(mc => mc.CustomerId)
				.ToListAsync();

			// Return only those not assigned anywhere
			var unassigned = allCustomers
				.Where(c => !assignedCustomerIds.Contains(c.Id))
				.ToList();

			return unassigned;
		}

		public async Task SaveManagersAsync(IEnumerable<SalesManagerEntity> managers)
		{
			foreach (var manager in managers)
			{
				var existing = await _context.SalesManagers.FindAsync(manager.Id);
				if (existing == null)
					_context.SalesManagers.Add(manager);
				else
					_context.Entry(existing).CurrentValues.SetValues(manager);
			}
			await _context.SaveChangesAsync();
		}


	}


}
