using Infrastructure.Contexts;
using Infrastructure.Entities;

namespace Infrastructure.Repositories
{
	public class ReturnRepository : Repo<DatabaseContext, ReturnEntity>
	{
		public ReturnRepository(DatabaseContext context) : base(context)
		{
		}
	}
}
