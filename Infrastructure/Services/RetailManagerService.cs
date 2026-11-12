using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class RetailManagerService(IDbContextFactory<DatabaseContext> factory, IPermissionChecker permissions, ICurrentUserContext session)
{
    private void EnsureOrders()
    {
        if (!session.IsLoggedIn || !permissions.Has(PermissionCatalog.Orders))
            throw new UnauthorizedAccessException("Нет доступа к оформлению заказов.");
    }

    private static IQueryable<SalesManagerEntity> OwnManagers(DatabaseContext db, int userId) =>
        db.SalesManagers.Where(m => m.Active && m.UserId == userId && m.User!.IsManager);

    public virtual async Task<RetailManagerState> LoadAsync()
    {
        EnsureOrders();
        var userId = session.UserId;
        var allManagers = permissions.Has(PermissionCatalog.OrdersViewAllManagers);
        var allCustomers = permissions.Has(PermissionCatalog.CustomersViewAll);
        await using var db = await factory.CreateDbContextAsync();
        var own = await OwnManagers(db, userId).AsNoTracking().SingleOrDefaultAsync();
        var managers = allManagers ? db.SalesManagers.Where(m => m.Active && (m.UserId == null || m.User!.IsManager))
            : OwnManagers(db, userId);
        var managerList = await managers.AsNoTracking().OrderBy(m => m.FullName).ToListAsync();
        var customers = db.Customers.AsQueryable();
        if (!allManagers && !(own == null && allCustomers))
            customers = customers.Where(c => OwnManagers(db, userId).Any(m => m.Id == c.SalesManagerId));
        var customerList = await customers.AsNoTracking().Include(c => c.PriceLevel).Include(c => c.Payments).ToListAsync();
        EnsureOrders();
        if (session.UserId != userId || allManagers != permissions.Has(PermissionCatalog.OrdersViewAllManagers) ||
            allCustomers != permissions.Has(PermissionCatalog.CustomersViewAll))
            throw new UnauthorizedAccessException("Пользователь или права доступа изменились. Повторно откройте заказы.");
        return new(userId, own?.Id, allManagers, allCustomers, managerList.Select(m => (SalesManager)m).ToList(), customerList);
    }

    public virtual async Task EnsureCustomerAccessAsync(string customerId)
    {
        EnsureOrders();
        var userId = session.UserId;
        var allManagers = permissions.Has(PermissionCatalog.OrdersViewAllManagers);
        var allCustomers = permissions.Has(PermissionCatalog.CustomersViewAll);
        await using var db = await factory.CreateDbContextAsync();
        var own = OwnManagers(db, userId);
        var unrestricted = allManagers || (allCustomers && !await own.AnyAsync());
        if (!await db.Customers.AnyAsync(c => c.Id == customerId &&
            (unrestricted || own.Any(m => m.Id == c.SalesManagerId))))
            throw new UnauthorizedAccessException("Нет доступа к клиенту выбранного менеджера. Обновите форму заказа.");
        EnsureOrders();
        if (session.UserId != userId || allManagers != permissions.Has(PermissionCatalog.OrdersViewAllManagers) ||
            allCustomers != permissions.Has(PermissionCatalog.CustomersViewAll))
            throw new UnauthorizedAccessException("Пользователь или права изменились. Повторно откройте заказы.");
    }
}
