using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
	public class BrandRepository : Repo<DatabaseContext, BrandEntity>
    {
        public BrandRepository(IDbContextFactory <DatabaseContext> contextFactory) : base(contextFactory)
        {
        }
    }
}
