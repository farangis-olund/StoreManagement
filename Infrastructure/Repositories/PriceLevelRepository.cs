using Infrastructure.Contexts;
using Infrastructure.Entities;


namespace Infrastructure.Repositories
{
	public class PriceLevelRepository : Repo<DatabaseContext, PriceLevelEntity>
    {
        public PriceLevelRepository(DatabaseContext context) : base(context)
        {
        }
    }
}
