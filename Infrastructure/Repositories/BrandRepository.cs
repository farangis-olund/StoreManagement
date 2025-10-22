using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class BrandRepository : Repo<DatabaseContext, BrandEntity>
{
	private readonly IDbContextFactory<DatabaseContext> _contextFactory;

	public BrandRepository(IDbContextFactory<DatabaseContext> contextFactory)
		: base(contextFactory)
	{
		_contextFactory = contextFactory;
	}

	/// <summary>
	/// Provides a queryable BrandEntity set for advanced queries (e.g. Include, filtering).
	/// </summary>
	public IQueryable<BrandEntity> Query()
	{
		var context = _contextFactory.CreateDbContext();
		return context.Brands.AsQueryable();
	}

	/// <summary>
	/// Returns all brands with Category included.
	/// </summary>
	public async Task<IEnumerable<BrandEntity>> GetAllWithCategoryAsync()
	{
		await using var context = _contextFactory.CreateDbContext();

		return await context.Brands
			.Include(b => b.Category)
			.OrderBy(b => b.BrandName)
			.ToListAsync();
	}
}
