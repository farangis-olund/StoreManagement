

using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class CourierRepository : Repo<DatabaseContext, CourierEntity>
{
	public CourierRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
	{

	}


}
