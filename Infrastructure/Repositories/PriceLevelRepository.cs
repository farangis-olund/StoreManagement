using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;


namespace Infrastructure.Repositories
{
	public class PriceLevelRepository : Repo<DatabaseContext, PriceLevelEntity>
    {
        public PriceLevelRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
		{
        }
    }
}
