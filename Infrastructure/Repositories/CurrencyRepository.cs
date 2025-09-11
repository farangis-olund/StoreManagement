
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;



namespace Infrastructure.Repositories
{
    public class CurrencyRepository : Repo<DatabaseContext, CurrencyEntity>
    {
        public CurrencyRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
		{

        }
    }
}
