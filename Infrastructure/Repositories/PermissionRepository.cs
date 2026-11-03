using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;


public class PermissionRepository : Repo<DatabaseContext, PermissionEntity>
{
    public PermissionRepository(IDbContextFactory<DatabaseContext> contextFactory)
        : base(contextFactory)
    {
    }
}
