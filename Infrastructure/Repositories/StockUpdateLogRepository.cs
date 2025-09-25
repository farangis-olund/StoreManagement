using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class StockUpdateLogRepository : Repo<DatabaseContext, StockUpdateLogEntity>
    {
    public StockUpdateLogRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
    {
    }
    public override async Task<IEnumerable<StockUpdateLogEntity>> GetAllAsync()
    {
        using var context = CreateContext();
        try
        {
            return await context.StockUpdateLog
                                .OrderBy(x => x.UpdateDate)
                                .ToListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting entities of type {nameof(StockUpdateLogEntity)}: {ex.Message}");
            return Enumerable.Empty<StockUpdateLogEntity>();
        }
    }

    public override async Task<StockUpdateLogEntity?> GetOneAsync(
        Expression<Func<StockUpdateLogEntity, bool>> predicate,
        Func<Task<StockUpdateLogEntity?>>? createIfNotFound = null)
    {
        using var context = CreateContext();
        try
        {
            var entity = await context.StockUpdateLog.FirstOrDefaultAsync(predicate);
            if (entity is not null) return entity;

            if (createIfNotFound is null) return null;

            var created = await createIfNotFound();
            if (created is null) return null;

            context.StockUpdateLog.Add(created);
            await context.SaveChangesAsync();
            return created;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting/creating entity {nameof(StockUpdateLogEntity)}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ExistsAsync(DateTime date)
    {
        var d = date.Date;
        using var context = CreateContext();
        return await context.StockUpdateLog.AnyAsync(x => x.UpdateDate == d);
    }

    public async Task<StockUpdateLogEntity> AddAsync(StockUpdateLogEntity entity)
    {
        entity.UpdateDate = entity.UpdateDate.Date;
        using var context = CreateContext();
        context.StockUpdateLog.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task<StockUpdateLogEntity> AddIfNotExistsAsync(DateTime date, string? comment = null)
    {
        var d = date.Date;
        using var context = CreateContext();
        var existing = await context.StockUpdateLog.FirstOrDefaultAsync(x => x.UpdateDate == d);
        if (existing is not null) return existing;

        var entity = new StockUpdateLogEntity { UpdateDate = d, Comment = comment };
        context.StockUpdateLog.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task<List<DateTime>> GetUpdatedDatesAsync()
    {
        using var context = CreateContext();
        return await context.StockUpdateLog
                            .Select(x => x.UpdateDate)
                            .ToListAsync();
    }

    public async Task<int> DeleteByDateAsync(DateTime date)
    {
        var d = date.Date;
        using var context = CreateContext();
        var rows = await context.StockUpdateLog.Where(x => x.UpdateDate == d).ToListAsync();
        if (rows.Count == 0) return 0;
        context.StockUpdateLog.RemoveRange(rows);
        return await context.SaveChangesAsync();
    }
}
