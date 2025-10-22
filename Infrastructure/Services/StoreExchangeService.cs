
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

    // ─── Add record + update stock + cleanup old ─────
    public async Task AddAsync(StoreExchangeEntity exchange, CancellationToken ct = default)
    {
        if (exchange == null)
            throw new ArgumentNullException(nameof(exchange));

        _context.StoreExchanges.Add(exchange);
        await _context.SaveChangesAsync(ct);

        //  Update product stock based on the type
        if (exchange.ExchangeType == "получение_товара")
        {
            // Your store RECEIVES product → increase stock
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "+", ct);
        }
        else if (exchange.ExchangeType == "передача_товара")
        {
            // Your store SENDS product → decrease stock
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "-", ct);
        }


        //  Update product stock based on the type
        if (exchange.ExchangeType == "получение_возврата")
        {
            // Your store RECEIVES product → increase stock
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "+", ct);

            // Cleanup: remove corresponding "передача_товара" entries (already returned)
            await RemoveExchangeRecordsAsync(exchange.StoreCode, exchange.ArticleNumber, "передача_товара", exchange.Quantity, ct);
        }
        else if (exchange.ExchangeType == "погащение_долга")
        {
            // Your store SENDS product → decrease stock
            await _productService.UpdateProductQuantityAsync(exchange.Quantity, exchange.ArticleNumber, "-", ct);

            // Cleanup: remove corresponding "получение_товара" entries (already repaid)
            await RemoveExchangeRecordsAsync(exchange.StoreCode, exchange.ArticleNumber, "получение_товара", exchange.Quantity, ct);
        }
    }

    // ─── Remove (or decrement) old exchange entries ──
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