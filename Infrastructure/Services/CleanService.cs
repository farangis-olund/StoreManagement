using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class CleanService
{
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;

    public CleanService(IDbContextFactory<DatabaseContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task DeleteAllAsync()
    {
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

        await transaction.CommitAsync();
    }
}
