using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

public class ReturnReasonService
{
	private readonly IDbContextFactory<DatabaseContext> _dbFactory;

	public ReturnReasonService(IDbContextFactory<DatabaseContext> dbFactory)
	{
		_dbFactory = dbFactory;
	}

	public async Task<List<ReturnReasonEntity>> GetAllAsync()
	{
		using var db = _dbFactory.CreateDbContext();
		
		return await db.ReturnReasons
		.Where(r => r.IsActive)
		.OrderBy(r => r.Name == "Другое") // false (0) first, true (1) last
		.ThenBy(r => r.Name)
		.ToListAsync();

	}

	public async Task<ReturnReasonEntity> AddAsync(ReturnReasonEntity reason)
	{
		using var db = _dbFactory.CreateDbContext();
		db.ReturnReasons.Add(reason);
		await db.SaveChangesAsync();
		return reason;
	}

	public async Task UpdateAsync(ReturnReasonEntity reason)
	{
		using var db = _dbFactory.CreateDbContext();
		db.ReturnReasons.Update(reason);
		await db.SaveChangesAsync();
	}

	public async Task DeleteAsync(int id)
	{
		using var db = _dbFactory.CreateDbContext();
		var reason = await db.ReturnReasons.FindAsync(id);
		if (reason != null)
		{
			db.ReturnReasons.Remove(reason);
			await db.SaveChangesAsync();
		}
	}
}
