using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class StoreRepository : Repo<DatabaseContext, StoreEntity>
{
    public StoreRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
    {

    }


}
