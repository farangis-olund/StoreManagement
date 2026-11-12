using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

// Manager profiles are written by UserService; this service maintains assignments only.
public class ManagerService(IDbContextFactory<DatabaseContext> factory)
{
    public virtual async Task<List<SalesManagerEntity>> GetManagersAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SalesManagers.AsNoTracking().Include(m => m.User).OrderBy(m => m.Id).ToListAsync();
    }
    public async Task<SalesManagerEntity?> GetManagerByIdAsync(string id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SalesManagers.AsNoTracking().Include(m => m.ManagerCustomers)
            .Include(m => m.ManagerBrands).ThenInclude(mb => mb.Brand).SingleOrDefaultAsync(m => m.Id == id);
    }
    public async Task<List<ManagerBrandEntity>> GetManagerBrandsAsync(string managerId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ManagerBrands.AsNoTracking().Include(x => x.Brand).Where(x => x.ManagerId == managerId).ToListAsync();
    }
    public async Task AddAllBrandsAsync(string managerId, double salePercent)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.ManagerBrands.Where(x => x.ManagerId == managerId).Select(x => x.BrandId).ToListAsync();
        var brands = await db.Brands.Where(b => !existing.Contains(b.Id)).Select(b => b.Id).ToListAsync();
        db.ManagerBrands.AddRange(brands.Select(id => new ManagerBrandEntity { ManagerId = managerId, BrandId = id, SalesPercentage = salePercent }));
        await db.SaveChangesAsync();
    }
    public async Task DeleteAllBrandsAsync(string managerId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.ManagerBrands.Where(x => x.ManagerId == managerId).ExecuteDeleteAsync();
    }
    public async Task SaveManagerCustomersAsync(string managerId, IEnumerable<ManagerCustomerEntity> customers)
    {
        if (string.IsNullOrWhiteSpace(managerId)) return;
        var ids = customers.Select(c => c.CustomerId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await db.ManagerCustomers.Where(x => ids.Contains(x.CustomerId)).ExecuteDeleteAsync();
        db.ManagerCustomers.AddRange(ids.Select(id => new ManagerCustomerEntity { ManagerId = managerId, CustomerId = id }));
        // Update only the assignment so old editor objects cannot overwrite other customer fields.
        await db.Customers.Where(c => ids.Contains(c.Id)).ExecuteUpdateAsync(s => s.SetProperty(c => c.SalesManagerId, managerId));
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task SaveManagerBrandsAsync(string managerId, IEnumerable<ManagerBrandEntity> brands)
    {
        var rows = brands.GroupBy(b => b.BrandId).Select(g => new ManagerBrandEntity
            { ManagerId = managerId, BrandId = g.Key, SalesPercentage = g.First().SalesPercentage }).ToList();
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ManagerBrands.Where(x => x.ManagerId == managerId).ExecuteDeleteAsync();
        db.ManagerBrands.AddRange(rows);
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task<List<CustomerEntity>> GetUnassignedCustomersAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Customers.AsNoTracking().Where(c => c.SalesManagerId == null).ToListAsync();
    }
    public async Task DeleteCustomerAsync(string managerId, string customerId)
    {
        if (string.IsNullOrWhiteSpace(managerId) || string.IsNullOrWhiteSpace(customerId)) return;
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ManagerCustomers.Where(mc => mc.ManagerId == managerId && mc.CustomerId == customerId).ExecuteDeleteAsync();
        await db.Customers.Where(c => c.Id == customerId && c.SalesManagerId == managerId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.SalesManagerId, (string?)null));
        await tx.CommitAsync();
    }
    public async Task DeleteAllCustomersAsync(string managerId)
    {
        if (string.IsNullOrWhiteSpace(managerId)) return;
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ManagerCustomers.Where(mc => mc.ManagerId == managerId).ExecuteDeleteAsync();
        await db.Customers.Where(c => c.SalesManagerId == managerId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.SalesManagerId, (string?)null));
        await tx.CommitAsync();
    }
}
