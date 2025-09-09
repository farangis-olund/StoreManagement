using Infrastructure.Contexts;
using Infrastructure.Entities;

namespace Infrastructure.Repositories
{
	public class ReturnDetailRepository : Repo<DatabaseContext, ReturnDetailEntity>
	{
		public ReturnDetailRepository(DatabaseContext context) : base(context)
		{
		}
	}
}
