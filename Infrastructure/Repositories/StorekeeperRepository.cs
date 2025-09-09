

using Infrastructure.Contexts;
using Infrastructure.Entities;

namespace Infrastructure.Repositories;

public class StorekeeperRepository : Repo<DatabaseContext, StorekeeperEntity>
{
	public StorekeeperRepository(DatabaseContext context) : base(context)
	{

	}


}
