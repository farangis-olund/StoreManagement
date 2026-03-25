using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace Infrastructure.Repositories;


public class PermissionRepository : Repo<DatabaseContext, PermissionEntity>
{
    public PermissionRepository(IDbContextFactory<DatabaseContext> contextFactory)
        : base(contextFactory)
    {
    }

    /// <summary>
    /// Completely replaces all permissions with the provided list.
    /// </summary>
    public async Task ReplaceAllAsync(List<PermissionEntity> newList)
    {
        using var ctx = await _contextFactory.CreateDbContextAsync();

        var existing = await ctx.Permissions.AsNoTracking().ToListAsync();
        ctx.Permissions.RemoveRange(existing);

        await ctx.Permissions.AddRangeAsync(newList);
        await ctx.SaveChangesAsync();
    }
}
