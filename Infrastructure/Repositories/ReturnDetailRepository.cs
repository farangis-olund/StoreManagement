using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
	public class ReturnDetailRepository : Repo<DatabaseContext, ReturnDetailEntity>
	{
		public ReturnDetailRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
		{
		}
	}
}
