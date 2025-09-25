using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class OrganizationInfoRepository : Repo<DatabaseContext, OrganizationInfoEntity>
{
    public OrganizationInfoRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
    {
    }
}
