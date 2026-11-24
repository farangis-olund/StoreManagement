
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class ProductRepository : Repo<DatabaseContext, ProductEntity>
{
    
    public ProductRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
	{
       
    }

    public override async Task<IEnumerable<ProductEntity>> GetAllAsync()
    {
		using var context = CreateContext();
		try
        {
            List<ProductEntity> productList = await context.Products
            .Include(i => i.Brand)
            .Include(i => i.Group)
            .ToListAsync();

            var clearance = (await context.ClearanceProducts.Select(x => x.ArticleNumber).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var product in productList) product.IsClearance = clearance.Contains(product.ArticleNumber);
            await Infrastructure.Services.ProductExpiryCatalog.AnnotateAsync(context, productList);
            return productList;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting entities of type {typeof(ProductEntity).Name}: {ex.Message}");
            return Enumerable.Empty<ProductEntity>();
        }
       

    }

	public override async Task<ProductEntity?> GetOneAsync(
	 Expression<Func<ProductEntity, bool>> predicate,
	 Func<Task<ProductEntity>>? createIfNotFound = null)
	{
		using var context = CreateContext();
		try
		{
			var entity = await context.Products
				.Include(i => i.Brand)
				.Include(i => i.Group)
				.FirstOrDefaultAsync(predicate);

			if (entity == null && createIfNotFound != null)
			{
				entity = await createIfNotFound.Invoke();
				context.Set<ProductEntity>().Add(entity);
				await context.SaveChangesAsync();
			}

            if (entity != null)
            {
                entity.IsClearance = await context.ClearanceProducts.AnyAsync(x => x.ArticleNumber == entity.ArticleNumber);
                await Infrastructure.Services.ProductExpiryCatalog.AnnotateAsync(context, [entity]);
            }
			return entity;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting entity of type {typeof(ProductEntity).Name} by predicate: {ex.Message}");
			return null!;
		}
	}


	public async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string searchKeyword)
    {
		using var context = CreateContext();
		try
        {
            IQueryable<ProductEntity> query = context.Products;
            // Parameterize each word outside the expression; every word must match a field.
            foreach (var word in searchKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var pattern = $"%{word}%";
                query = query.Where(p => EF.Functions.Like(p.ArticleNumber, pattern) ||
                    EF.Functions.Like(p.ProductName, pattern) || EF.Functions.Like(p.Model, pattern) ||
                    EF.Functions.Like(p.Alternative, pattern) || EF.Functions.Like(p.Group.GroupName, pattern) ||
                    EF.Functions.Like(p.Brand.BrandName, pattern));
            }
            var searchResults = await query.Include(p => p.Brand).Include(p => p.Group).ToListAsync();

            var clearance = (await context.ClearanceProducts.Select(x => x.ArticleNumber).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var product in searchResults) product.IsClearance = clearance.Contains(product.ArticleNumber);
            await Infrastructure.Services.ProductExpiryCatalog.AnnotateAsync(context, searchResults);
            return searchResults;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error searching entity of type {typeof(ProductEntity).Name} by id: {ex.Message}");
            return null!;
        }
       
    }
}
