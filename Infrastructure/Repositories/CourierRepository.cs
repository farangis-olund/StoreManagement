

using Infrastructure.Contexts;
using Infrastructure.Entities;

namespace Infrastructure.Repositories;

public class CourierRepository : Repo<DatabaseContext, CourierEntity>
{
	public CourierRepository(DatabaseContext context) : base(context)
	{

	}


}
