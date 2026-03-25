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
        using var db = await _dbFactory.CreateDbContextAsync();

        // CHILD tables first
        db.OrderDetails.RemoveRange(db.OrderDetails);
        db.Payments.RemoveRange(db.Payments);
        db.ReturnDetails.RemoveRange(db.ReturnDetails);
        db.StockUpdateLog.RemoveRange(db.StockUpdateLog);
        db.StockMovements.RemoveRange(db.StockMovements);
        db.StockImportErrors.RemoveRange(db.StockImportErrors);

        // PARENT tables
        db.Returns.RemoveRange(db.Returns);
        db.Orders.RemoveRange(db.Orders);
        db.StoreExchanges.RemoveRange(db.StoreExchanges);

        await db.SaveChangesAsync();
    }
}
