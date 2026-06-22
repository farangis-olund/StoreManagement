using DocumentFormat.OpenXml.InkML;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace Infrastructure.Services;

public class ProductService
{
    private readonly ProductRepository _productRepository;
    private readonly BrandService _brandService;
    private readonly GroupService _groupService;
    private readonly ILogger<ProductService> _logger;
	private readonly DatabaseContext _db;
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;
    public ProductService(ProductRepository productRepository,
                          BrandService brandService,
                          GroupService groupService,
                          ILogger<ProductService> logger,
                          DatabaseContext db, IDbContextFactory<DatabaseContext> dbFactory)
    {
        
        _productRepository = productRepository;
        _brandService = brandService;
        _groupService = groupService;
        _logger = logger;
        _db = db;
        _dbFactory = dbFactory;
    }

    public async Task<Product> AddProductAsync(ProductEntity product)
    {
        try
        {
            var existingProduct = await _productRepository.GetOneAsync(p => p.ArticleNumber == product.ArticleNumber);
            if (existingProduct != null)
            {
                return null!;
            }

            return await _productRepository.AddAsync(product);
           
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding product: {ex.Message}");
            Debug.WriteLine($"Error getting product: {ex.Message}");
            return null!;
        }
        
    }
	public async Task<decimal> GetDefaultPriceAsync(string article)
	{
		var product = await GetProductByArticleAsync(article);
		return product?.PriceLevel1 ?? 0m;
	}


	public async Task<Product> GetProductByArticleAsync(string articleNumber)
    {
        try
        {
            return await _productRepository.GetOneAsync(p => p.ArticleNumber == articleNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting product: {ex.Message}");
            Debug.WriteLine($"Error getting product: {ex.Message}");
            return null!;
        }
    }

    public async Task<IEnumerable<Product>> GetAllProductAsync()
    {
        try
        {
            var productEntities = await _productRepository.GetAllAsync();

            return productEntities
                .OrderBy(p => p.Numbering)   
                .Select(productEntity => (Product)productEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving product variant: {ex.Message}");
            Debug.WriteLine(ex.Message);
            return [];
        }
    }

    public async Task<IEnumerable<Product>> GetAllProductAsync(
	bool onlyAvailable = false,
	CancellationToken ct = default)
	{
		try
		{
			// If this service shares the same DbContext in the scope, clear tracker to avoid cached values
			_db.ChangeTracker.Clear();

			IQueryable<ProductEntity> q = _db.Products.AsNoTracking();

			if (onlyAvailable)
				q = q.Where(p => p.Quentity > 0);

			var entities = await q.ToListAsync(ct);

			// map to your DTO using the implicit operator
			return entities.Select(e => (Product)e).ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving products");
			Debug.WriteLine(ex.Message);
			return Array.Empty<Product>();
		}
	}

	public async Task<Product> UpdateProductAsync(ProductEntity product)
    {
        try
        {            
            return await _productRepository.UpdateAsync(p => p.ArticleNumber == product.ArticleNumber, product);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in updating product: {ex.Message}");
            Debug.WriteLine($"Error in updating product: {ex.Message}");
            return null!;
        }
    }

    public async Task<bool> DeleteProductByArticleAsync(string articleNumber)
    {

        try
        {
            await _productRepository.RemoveAsync(p => p.ArticleNumber == articleNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in deleting product: {ex.Message}");
            Debug.WriteLine($"Error in deleting product: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<Product>> SearchAndGetProductsAsync(string searchWord)
    {
        try
        {
            var productEntities = await _productRepository.SearchProductsAsync(searchWord);
            return productEntities.Select(productEntity => (Product)productEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving product variant: {ex.Message}");
            Debug.WriteLine(ex.Message);
            return [];
        }
    }

	public async Task<StockDeductionResult> DeductStockAsync(
	IEnumerable<StockDeductionItem> items,
	DatabaseContext db,
	CancellationToken ct = default)
	{
		var result = new StockDeductionResult();

		foreach (var i in items)
		{
			var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE Products
            SET Quentity = Quentity - {i.Quantity}
            WHERE ArticleNumber = {i.ArticleNumber} AND Quentity >= {i.Quantity};
        ", ct);

			if (affected == 0)
				result.NotEnoughArticles.Add(i.ArticleNumber);
		}

		return result;  // result.Success is computed from NotEnoughArticles.Count == 0
	}

    public async Task<StockDeductionResult> DeductStockAsync(
    IEnumerable<StockDeductionItem> items,
    CancellationToken ct = default)
    {
        var result = new StockDeductionResult();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        foreach (var i in items)
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE Products
            SET Quentity = Quentity - {i.Quantity}
            WHERE ArticleNumber = {i.ArticleNumber} AND Quentity >= {i.Quantity};
        ", ct);

            if (affected == 0)
                result.NotEnoughArticles.Add(i.ArticleNumber);
        }

        return result; // result.Success is computed from NotEnoughArticles.Count == 0
    }

    // items: list of (Article, Qty) to add in one SaveChanges.
    public async Task<int> AddQuantitiesByArticlesAsync(
        IEnumerable<(string Article, int Qty)> items,
        CancellationToken ct = default)
    {
        var dict = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Article) && i.Qty != 0)
            .GroupBy(i => i.Article)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        if (dict.Count == 0) return 0;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var articles = dict.Keys.ToList();
        var products = await db.Products
            .Where(p => articles.Contains(p.ArticleNumber))
            .ToListAsync(ct);

        foreach (var p in products)
        {
            var add = dict[p.ArticleNumber];
            var current = p.Quentity;
            p.Quentity = Math.Max(0, current + add);
        }

        // If you want to know how many products were updated:
        return await db.SaveChangesAsync(ct);
    }


    public async Task<bool> ExistsByArticleAsync(string articleNumber)
    {
        return await _db.Products
            .AnyAsync(p => p.ArticleNumber == articleNumber);
    }


    // Insert movement (Receipt or WriteOff)
    public async Task InsertStockMovementAsync(
        int itemCount,
        DateTime date,
        string type,
        CancellationToken ct = default)
    {
        var entry = new StockMovementEntity
        {
            MovementDate = date,
            ItemCount = itemCount,
            MovementType = type, // "Receipt" or "WriteOff"
            CreatedAt = DateTime.UtcNow
        };

        _db.StockMovements.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    // Check if a stock movement of given type exists for date
    public async Task<bool> IsStockMovementExistAsync(
        DateTime date,
        string type,
        CancellationToken ct = default)
    {
        return await _db.StockMovements
            .AnyAsync(p => p.MovementDate == date && p.MovementType == type, ct);
    }

    // ✅ Get current product stock
    public async Task<int> GetProductQuantityAsync(string articleNumber, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber, ct);

        return product?.Quentity ?? 0;
    }

    // ✅ Update product stock (add / subtract)
    public async Task UpdateProductQuantityAsync(int qty, string articleNumber, string operation, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber, ct);
        if (product == null) return;

        if (operation == "+")
            product.Quentity += qty;
        else if (operation == "-")
            product.Quentity -= qty;

        await _db.SaveChangesAsync(ct);
    }

    // ✅ Delete all import errors
    public async Task ClearStockImportErrorsAsync(CancellationToken ct = default)
    {
        var all = _db.Set<StockImportErrorEntity>();
        _db.RemoveRange(all);
        await _db.SaveChangesAsync(ct);
    }

    // ✅ Insert missing article error
    public async Task AddStockImportErrorAsync(int qty, string articleNumber, CancellationToken ct = default)
    {
        var err = new StockImportErrorEntity
        {
            ArticleNumber = articleNumber,
            Quantity = qty
           
        };
        _db.Add(err);
        await _db.SaveChangesAsync(ct);
    }

    // ✅ Get product by article
    public async Task<Product?> GetProductByArticleNumberAsync(string articleNumber, CancellationToken ct = default)
    {
        return await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber, ct);
    }

    public async Task<List<StockImportErrorEntity>> GetStockImportErrorsAsync(CancellationToken ct = default)
    {
        return await _db.Set<StockImportErrorEntity>()
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task UpdateWarehousePlaceAsync(string articleNumber, string newPlace, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber, ct);
        if (product == null) return;

        product.WarehousePlace = newPlace;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAllProductsAsync()
    {
        try
        {
            // Example if using EF Core:
            _db.Products.RemoveRange(_db.Products);
            await _db.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

}
