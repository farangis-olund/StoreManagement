using Infrastructure.Contexts;
using Infrastructure.Entities;


namespace Infrastructure.Repositories
{
	public class SalesManagerRepository : Repo<DatabaseContext, SalesManagerEntity>
    {
        public SalesManagerRepository(DatabaseContext context) : base(context)
        {
        }
    }
}
