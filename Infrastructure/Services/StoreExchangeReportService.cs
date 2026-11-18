using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class StoreExchangeReportService(IDbContextFactory<DatabaseContext> factory, IPermissionChecker permissions)
{
    public virtual async Task<IReadOnlyList<StoreExchangeReportRow>> GetAsync(CancellationToken ct = default)
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync(ct);
        // Exchange records already hold outstanding quantities: repayments reduce/remove them.
        var exchanges = db.StoreExchanges.AsNoTracking()
            .Where(x => x.Quantity > 0 &&
                (x.ExchangeType == "получение_товара" || x.ExchangeType == "передача_товара"))
            .GroupBy(x => x.ArticleNumber)
            .Select(g => new
            {
                ArticleNumber = g.Key,
                Incoming = g.Sum(x => x.ExchangeType == "получение_товара" ? (long)x.Quantity : 0L),
                Outgoing = g.Sum(x => x.ExchangeType == "передача_товара" ? (long)x.Quantity : 0L)
            });
        var totals = await (
            from product in db.Products.AsNoTracking()
            join exchange in exchanges on product.ArticleNumber equals exchange.ArticleNumber into productExchanges
            from exchange in productExchanges.DefaultIfEmpty()
            select new
            {
                product.ArticleNumber,
                WarehouseQuantity = product.Quentity,
                Incoming = (long?)exchange.Incoming ?? 0L,
                Outgoing = (long?)exchange.Outgoing ?? 0L
            }).ToListAsync(ct);
        EnsureAccess();
        return totals.OrderBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase)
            .Select((x, index) => new StoreExchangeReportRow(index + 1, x.ArticleNumber, x.WarehouseQuantity,
                x.Incoming, x.Outgoing)).ToArray();
    }

    private void EnsureAccess()
    {
        if (!permissions.Has(PermissionCatalog.Reports))
            throw new UnauthorizedAccessException("Нет права доступа к отчётам.");
    }
}
