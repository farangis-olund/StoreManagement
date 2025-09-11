

using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class StorekeeperRepository : Repo<DatabaseContext, StorekeeperEntity>
{
	public StorekeeperRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
	{

	}


}
