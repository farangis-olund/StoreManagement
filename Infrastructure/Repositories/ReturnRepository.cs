using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
	public class ReturnRepository : Repo<DatabaseContext, ReturnEntity>
	{
		public ReturnRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
		{
		}
	}
}
