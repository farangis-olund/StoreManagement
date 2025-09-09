using Infrastructure.Contexts;
using Infrastructure.Entities;

namespace Infrastructure.Repositories
{
	public class BrandRepository : Repo<DatabaseContext, BrandEntity>
    {
        public BrandRepository(DatabaseContext context) : base(context)
        {
        }
    }
}
