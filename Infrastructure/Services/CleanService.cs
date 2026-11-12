using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Constants;
using Infrastructure.Security;

namespace Infrastructure.Services;

public class CleanService
{
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;
    private readonly OfficialMonthlyPlanService? _monthlyPlans;
    private readonly AdminAccessPolicy _access;

    public CleanService(IDbContextFactory<DatabaseContext> dbFactory, AdminAccessPolicy access, OfficialMonthlyPlanService? monthlyPlans = null)
    {
        _dbFactory = dbFactory;
        _monthlyPlans = monthlyPlans;
        _access = access;
    }

    public async Task DeleteAllAsync()
    {
        _access.EnsureCanOpen(PermissionCatalog.AdminCleanup);
        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        // CourierPayments has a restrictive FK to Orders, so it must be removed first.
        // ExecuteDelete keeps large cleanup operations out of the change tracker.
        await db.CourierPayments.ExecuteDeleteAsync();
        await db.OrderDetails.ExecuteDeleteAsync();
        await db.Payments.ExecuteDeleteAsync();
        await db.ReturnDetails.ExecuteDeleteAsync();
        await db.StockUpdateLog.ExecuteDeleteAsync();
        await db.StockMovements.ExecuteDeleteAsync();
        await db.StockImportErrors.ExecuteDeleteAsync();

        await db.Returns.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
        await db.StoreExchanges.ExecuteDeleteAsync();

        _access.EnsureCanOpen(PermissionCatalog.AdminCleanup);
        await transaction.CommitAsync();
        _monthlyPlans?.NotifyChanged();
    }
}
