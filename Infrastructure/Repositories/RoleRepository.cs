using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class RoleRepository : Repo<DatabaseContext, RoleEntity>
{
    public RoleRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
    {

    }


}
