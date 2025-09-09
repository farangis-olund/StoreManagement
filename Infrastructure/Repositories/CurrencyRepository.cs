
using Infrastructure.Contexts;
using Infrastructure.Entities;



namespace Infrastructure.Repositories
{
    public class CurrencyRepository : Repo<DatabaseContext, CurrencyEntity>
    {
        public CurrencyRepository(DatabaseContext context)
            : base(context)
        {

        }
    }
}
