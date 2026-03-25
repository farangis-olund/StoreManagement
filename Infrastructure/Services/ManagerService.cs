
using Infrastructure.Contexts;
using Infrastructure.Dtos;
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
            if (string.IsNullOrWhiteSpace(managerId))
                return;

            var customerIds = customers.Select(c => c.CustomerId).ToList();

            // 🔹 1. Remove old manager links for these specific customers (so they won't be assigned to two managers)
            var oldLinks = _context.ManagerCustomers
                .Where(x => customerIds.Contains(x.CustomerId));
            _context.ManagerCustomers.RemoveRange(oldLinks);

            // 🔹 2. Add new manager-customer links
            await _context.ManagerCustomers.AddRangeAsync(customers);

            // 🔹 3. Update Customers table (set SaleManagerId)
            var affectedCustomers = await _context.Customers
                .Where(c => customerIds.Contains(c.Id))
                .ToListAsync();

            foreach (var customer in affectedCustomers)
            {
                customer.SalesManagerId = managerId;
                _context.Entry(customer).State = EntityState.Modified;
            }

            // 🔹 4. Save everything
            await _context.SaveChangesAsync();
        }



        public async Task SaveManagerBrandsAsync(string managerId, IEnumerable<ManagerBrandEntity> brands)
        {
            // 1️⃣ Remove existing
            var existing = await _context.ManagerBrands
                .Where(x => x.ManagerId == managerId)
                .ToListAsync();

            _context.ManagerBrands.RemoveRange(existing);

            // 🔥 IMPORTANT: Save removal first
            await _context.SaveChangesAsync();

            // 2️⃣ Remove duplicates in input
            var distinctBrands = brands
                .GroupBy(x => new { x.ManagerId, x.BrandId })
                .Select(g => g.First())
                .ToList();

            // 3️⃣ Add new ones
            await _context.ManagerBrands.AddRangeAsync(distinctBrands);

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

        public async Task<bool> DeleteManagerAsync(string managerId)
        {
            try
            {
                // 🔹 Load the manager (including related data)
                var manager = await _context.SalesManagers
                    .Include(m => m.ManagerCustomers)
                    .Include(m => m.ManagerBrands)
                    .FirstOrDefaultAsync(m => m.Id == managerId);

                if (manager == null)
                    return false;

                // 🔹 Remove related entities first (if any)
                if (manager.ManagerCustomers?.Any() == true)
                    _context.ManagerCustomers.RemoveRange(manager.ManagerCustomers);

                if (manager.ManagerBrands?.Any() == true)
                    _context.ManagerBrands.RemoveRange(manager.ManagerBrands);

                // 🔹 Remove the manager itself
                _context.SalesManagers.Remove(manager);

                // 🔹 Save changes
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting manager {managerId}: {ex.Message}");
                return false;
            }
        }

        // 🔹 Delete ONE customer from the selected manager
        public async Task DeleteCustomerAsync(string managerId, string customerId)
        {
            if (string.IsNullOrWhiteSpace(managerId) || string.IsNullOrWhiteSpace(customerId))
                return;

            // 1️⃣ Remove link from ManagerCustomers table
            var link = await _context.ManagerCustomers
                .FirstOrDefaultAsync(mc => mc.ManagerId == managerId && mc.CustomerId == customerId);

            if (link != null)
                _context.ManagerCustomers.Remove(link);

            // 2️⃣ Update Customer table
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer != null && customer.SalesManagerId == managerId)
            {
                customer.SalesManagerId = null; // unassign manager
                _context.Entry(customer).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
        }



        // 🔹 Delete ALL customers assigned to this manager
        public async Task DeleteAllCustomersAsync(string managerId)
        {
            if (string.IsNullOrWhiteSpace(managerId))
                return;

            // 1️⃣ Get all manager-customer links
            var links = await _context.ManagerCustomers
                .Where(mc => mc.ManagerId == managerId)
                .ToListAsync();

            if (links.Any())
                _context.ManagerCustomers.RemoveRange(links);

            // 2️⃣ Clear manager from customers table
            var customers = await _context.Customers
                .Where(c => c.SalesManagerId == managerId)
                .ToListAsync();

            foreach (var c in customers)
            {
                c.SalesManagerId = null;
                _context.Entry(c).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
        }

    }


}
