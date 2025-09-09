
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class ProductRepository : Repo<DatabaseContext, ProductEntity>
{
    new private readonly DatabaseContext _context;
    public ProductRepository(DatabaseContext context) 
        : base(context)
    {
        _context = context;
    }

    public override async Task<IEnumerable<ProductEntity>> GetAllAsync()
    {
        try
        {
            List<ProductEntity> productList = await _context.Products
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

    public override async Task<ProductEntity> GetOneAsync(Expression<Func<ProductEntity, bool>> predicate, Func<Task<ProductEntity>> createIfNotFound)
    {
        try
        {
            var entity = await _context.Products
                .Include (i => i.Brand)
                .Include(i => i.Group)
                .FirstOrDefaultAsync(predicate);

                entity = await createIfNotFound.Invoke();
                _context.Set<ProductEntity>().Add(entity);
             return entity;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting entity of type {typeof(ProductEntity).Name} by id: {ex.Message}");
            return null!;
        }
    }

    public async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string searchKeyword)
    {
        try
        {
            var searchWords = searchKeyword.Split(' ');
           
            var searchResults = await _context.Products
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
