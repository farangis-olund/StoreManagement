
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
            var searchWords = searchKeyword.Split(' ');
           
            var searchResults = await context.Products
                .Where(p =>
                    searchWords.All(word =>
                        EF.Functions.Like(p.ArticleNumber, $"%{word}%") ||
                        EF.Functions.Like(p.ProductName, $"%{word}%") ||
                        EF.Functions.Like(p.Model, $"%{word}%") ||
                        EF.Functions.Like(p.Alternative, $"%{word}%") ||
                        EF.Functions.Like(p.Group.GroupName, $"%{word}%") ||
                        EF.Functions.Like(p.Brand.BrandName, $"%{word}%")
                    )
                )
                .ToListAsync();

            return searchResults;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error searching entity of type {typeof(ProductEntity).Name} by id: {ex.Message}");
            return null!;
        }
       
    }
}
