using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;


namespace Infrastructure.Repositories
{
	public class SalesManagerRepository : Repo<DatabaseContext, SalesManagerEntity>
    {
        public SalesManagerRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
		{
        }
    }
}
