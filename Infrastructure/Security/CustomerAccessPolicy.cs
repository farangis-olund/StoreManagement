using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Security;

public sealed class CustomerAccessPolicy(IPermissionChecker permissions, ICurrentUserContext session)
{
    public bool CanViewAll => permissions.Has(PermissionCatalog.Customers) && permissions.Has(PermissionCatalog.CustomersViewAll);
    public int UserId => session.UserId;

    private void RequireModule()
    {
        if (!permissions.Has(PermissionCatalog.Customers)) throw new UnauthorizedAccessException("Нет права доступа к клиентам.");
    }

    public IQueryable<SalesManagerEntity> Managers(DatabaseContext db)
    {
        RequireModule();
        if (CanViewAll) return db.SalesManagers;
        var userId = session.IsLoggedIn ? session.UserId : 0;
        return db.SalesManagers.Where(m => m.UserId == userId && m.Active && m.User!.IsManager);
    }

    public IQueryable<CustomerEntity> Customers(DatabaseContext db)
    {
        RequireModule();
        if (CanViewAll) return db.Customers;
        var managers = Managers(db);
        return db.Customers.Where(c => managers.Any(m => m.Id == c.SalesManagerId));
    }

    public async Task EnsureAccessAsync(DatabaseContext db, string customerId)
    {
        if (!CanViewAll && !await Customers(db).AnyAsync(c => c.Id == customerId))
            throw new UnauthorizedAccessException("Нет доступа к этому клиенту. Разрешены только клиенты вашего менеджера.");
        RequireModule();
    }

    public async Task EnsureManagerAssignmentAsync(DatabaseContext db, string? managerId)
    {
        if (!CanViewAll && !await Managers(db).AnyAsync(m => m.Id == managerId))
            throw new UnauthorizedAccessException("Можно назначить клиенту только своего менеджера.");
        RequireModule();
    }
}
