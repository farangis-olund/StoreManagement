
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class StoreExchangeService
{
    private readonly DatabaseContext _context;
    private readonly ProductService _productService;

    public StoreExchangeService(DatabaseContext context, ProductService productService)
    {
        _context = context;
        _productService = productService;
    }


    // Get all records
    public async Task<List<StoreExchangeEntity>> GetAllAsync()
    {
        return await _context.StoreExchanges
            .Include(x => x.Store)
            .OrderByDescending(x => x.Id)
            .ToListAsync();
    }

    // Filter by store code
    public async Task<List<StoreExchangeEntity>> GetByStoreAsync(string storeCode)
    {
        return await _context.StoreExchanges
            .Where(x => x.StoreCode == storeCode)
            .Include(x => x.Store)
            .ToListAsync();
    }

    // Filter by type ("передача_товара" or "получение_товара")
    public async Task<List<StoreExchangeEntity>> GetByTypeAsync(string type)
    {
        return await _context.StoreExchanges
            .Where(x => x.ExchangeType == type)
            .Include(x => x.Store)
            .ToListAsync();
    }

    public async Task AddAsync(StoreExchangeEntity exchange, CancellationToken ct = default)
    {
        if (exchange == null)
            throw new ArgumentNullException(nameof(exchange));

        // Save repayment or borrow record first
        _context.StoreExchanges.Add(exchange);
        await _context.SaveChangesAsync(ct);

        // STOCK UPDATE
        if (exchange.ExchangeType == "получение_товара")
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "+", ct);

        else if (exchange.ExchangeType == "передача_товара")
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "-", ct);

        else if (exchange.ExchangeType == "получение_возврата")
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "+", ct);

        else if (exchange.ExchangeType == "погащение_долга")
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "-", ct);


        // SPECIAL RULES
        if (exchange.ExchangeType == "погащение_долга")
        {
            // 1️⃣ Remove from получение_товара (the debt)
            await RemoveExchangeRecordsAsync(exchange.StoreCode, exchange.ArticleNumber, "получение_товара", exchange.Quantity, ct);

            // 2️⃣ Remove this repayment entry itself
            _context.StoreExchanges.Remove(exchange);
            await _context.SaveChangesAsync(ct);

            // 3️⃣ Remove all if debt fully repaid
            await RemoveAllIfBalancedAsync(exchange.StoreCode, exchange.ArticleNumber, ct);
            return;
        }

        if (exchange.ExchangeType == "получение_возврата")
        {
            // Remove corresponding передача_товара
            await RemoveExchangeRecordsAsync(exchange.StoreCode, exchange.ArticleNumber, "передача_товара", exchange.Quantity, ct);

            // And check if everything balances
            await RemoveAllIfBalancedAsync(exchange.StoreCode, exchange.ArticleNumber, ct);
            return;
        }
    }

    private async Task RemoveAllIfBalancedAsync(
    string storeCode,
    string article,
    CancellationToken ct)
    {
        int totalReceived = await _context.StoreExchanges
            .Where(x => x.StoreCode == storeCode &&
                        x.ArticleNumber == article &&
                        x.ExchangeType == "получение_товара")
            .SumAsync(x => x.Quantity, ct);

        int totalRepaid = await _context.StoreExchanges
            .Where(x => x.StoreCode == storeCode &&
                        x.ArticleNumber == article &&
                        x.ExchangeType == "погащение_долга")
            .SumAsync(x => x.Quantity, ct);

        if (totalReceived == totalRepaid)
        {
            // Remove everything for this store/article
            var all = await _context.StoreExchanges
                .Where(x => x.StoreCode == storeCode &&
                            x.ArticleNumber == article)
                .ToListAsync(ct);

            _context.StoreExchanges.RemoveRange(all);
            await _context.SaveChangesAsync(ct);
        }
    }

    private async Task RemoveExchangeRecordsAsync(
        string storeCode,
        string articleNumber,
        string typeToRemove,
        int quantityToRemove,
        CancellationToken ct = default)
    {
        var records = await _context.StoreExchanges
            .Where(x => x.StoreCode == storeCode &&
                        x.ArticleNumber == articleNumber &&
                        x.ExchangeType == typeToRemove)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        int remaining = quantityToRemove;

        foreach (var rec in records)
        {
            if (remaining <= 0)
                break;

            if (rec.Quantity <= remaining)
            {
                // Remove fully
                remaining -= rec.Quantity;
                _context.StoreExchanges.Remove(rec);
            }
            else
            {
                // Reduce partially
                rec.Quantity -= remaining;
                remaining = 0;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private readonly Dictionary<string, string> debtPairs = new()
{
    { "получение_возврата", "передача_товара" },
    { "погащение_долга", "получение_товара" }
};

    //private async Task ApplyDebtCleanupAsync(StoreExchangeEntity exchange, CancellationToken ct)
    //{
    //    if (!debtPairs.TryGetValue(exchange.ExchangeType, out string debtType))
    //        return;

    //    var debtRecords = await _context.StoreExchanges
    //        .Where(x => x.StoreCode == exchange.StoreCode &&
    //                    x.ArticleNumber == exchange.ArticleNumber &&
    //                    x.ExchangeType == debtType)
    //        .OrderBy(x => x.Id)
    //        .ToListAsync(ct);

    //    int originalDebtCount = debtRecords.Count();   // <-- important

    //    int remaining = exchange.Quantity;

    //    foreach (var rec in debtRecords)
    //    {
    //        if (remaining <= 0) break;

    //        if (rec.Quantity > remaining)
    //        {
    //            rec.Quantity -= remaining;
    //            remaining = 0;
    //        }
    //        else
    //        {
    //            remaining -= rec.Quantity;
    //            _context.StoreExchanges.Remove(rec);
    //        }
    //    }

    //    await _context.SaveChangesAsync(ct);

    //    // 🟥 If no debt rows existed → repayment is meaningless → remove it
    //    if (originalDebtCount == 0)
    //    {
    //        _context.StoreExchanges.Remove(exchange);
    //        await _context.SaveChangesAsync(ct);
    //        return;
    //    }
    //}


    //private async Task RemoveFullyRepaidDebtAsync(string store, string article, CancellationToken ct)
    //{
    //    int outgoing = await _context.StoreExchanges
    //        .Where(x => x.StoreCode == store &&
    //                    x.ArticleNumber == article &&
    //                    x.ExchangeType == "передача_товара")
    //        .SumAsync(x => x.Quantity, ct);

    //    int incoming = await _context.StoreExchanges
    //        .Where(x => x.StoreCode == store &&
    //                    x.ArticleNumber == article &&
    //                    x.ExchangeType == "получение_возврата")
    //        .SumAsync(x => x.Quantity, ct);

    //    int loan = await _context.StoreExchanges
    //        .Where(x => x.StoreCode == store &&
    //                    x.ArticleNumber == article &&
    //                    x.ExchangeType == "получение_товара")
    //        .SumAsync(x => x.Quantity, ct);

    //    int repayment = await _context.StoreExchanges
    //        .Where(x => x.StoreCode == store &&
    //                    x.ArticleNumber == article &&
    //                    x.ExchangeType == "погащение_долга")
    //        .SumAsync(x => x.Quantity, ct);

    //    bool loanBalanced = outgoing == incoming;
    //    bool repaymentBalanced = loan == repayment;

    //    if (loanBalanced && repaymentBalanced)
    //    {
    //        var all = _context.StoreExchanges
    //            .Where(x => x.StoreCode == store &&
    //                        x.ArticleNumber == article)
    //            .ToList();

    //        _context.StoreExchanges.RemoveRange(all);
    //        await _context.SaveChangesAsync(ct);
    //    }
    //}



    public async Task<IEnumerable<ExchangeProductDto>> GetExchangeProductsAsync(string storeCode, string exchangeType)
    {
        if (string.IsNullOrWhiteSpace(storeCode))
            throw new ArgumentException("Store code cannot be empty", nameof(storeCode));

        var query = _context.StoreExchanges
            .Include(e => e.Product)
            .ThenInclude(p => p.Brand)
            .Where(e => e.StoreCode == storeCode && e.ExchangeType == exchangeType)
            .GroupBy(e => new
            {
                e.Product.ArticleNumber,
                e.Product.ProductName,
                e.Product.Brand.BrandName,
                e.Product.Marka,
                e.Product.Model,
                e.Product.WarehousePlace,
                e.StoreCode
            })
            .Select(g => new ExchangeProductDto
            {
                ArticleNumber = g.Key.ArticleNumber,
                ProductName = g.Key.ProductName,
                BrandName = g.Key.BrandName,
                Marka = g.Key.Marka,
                Model = g.Key.Model,
                StoreCode = g.Key.StoreCode,
                WarehousePlace = g.Key.WarehousePlace,
                Quantity = g.Sum(x => x.Product.Quentity),
                Debt = g.Sum(x => x.Quantity) // you can adjust if you track actual debts separately
            });

        return await query.ToListAsync();
    }


}