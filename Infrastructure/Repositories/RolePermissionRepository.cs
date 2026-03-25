using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class RolePermissionRepository
{
    private readonly IDbContextFactory<DatabaseContext> _factory;

    public RolePermissionRepository(IDbContextFactory<DatabaseContext> factory)
    {
        _factory = factory;
    }

    // Get all permission IDs assigned to a role
    public async Task<List<int>> GetPermissionIdsAsync(int roleId)
    {
        using var db = await _factory.CreateDbContextAsync();

        return await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync();
    }

    // Remove all permissions for a role
    public async Task ClearPermissionsAsync(int roleId)
    {
        using var db = await _factory.CreateDbContextAsync();

        var items = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        db.RolePermissions.RemoveRange(items);
        await db.SaveChangesAsync();
    }

    // Add permissions to a role
    public async Task AddPermissionsAsync(int roleId, IEnumerable<int> permIds)
    {
        using var db = await _factory.CreateDbContextAsync();

        foreach (var id in permIds)
        {
            db.RolePermissions.Add(new RolePermissionEntity
            {
                RoleId = roleId,
                PermissionId = id
            });
        }

        await db.SaveChangesAsync();
    }
}