using Infrastructure.Constants;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public static class PermissionCatalogSynchronizer
{
    public static void Synchronize(DbContext context)
    {
        // Detect duplicate catalogue keys before making any database changes.
        var definitions = PermissionCatalog.All.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        using var transaction = context.Database.BeginTransaction();

        // Hold update/range locks until commit so simultaneous startups cannot insert duplicates.
        var existing = context.Set<PermissionEntity>()
            .FromSqlRaw("SELECT [Id], [Key], [Description] FROM [dbo].[Permissions] WITH (UPDLOCK, HOLDLOCK)")
            .AsTracking()
            .ToList()
            .ToLookup(p => p.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions.Values)
        {
            if (!existing.Contains(definition.Key))
            {
                context.Set<PermissionEntity>().Add(new PermissionEntity
                {
                    Key = definition.Key,
                    Description = definition.Description
                });
                continue;
            }

            // Preserve every existing ID, including legacy duplicates with role assignments.
            foreach (var permission in existing[definition.Key])
            {
                permission.Key = definition.Key;
                permission.Description = definition.Description;
            }
        }

        // Unknown keys may belong to a newer app version. Never delete permissions or grants here.
        context.SaveChanges();
        transaction.Commit();
    }
}
