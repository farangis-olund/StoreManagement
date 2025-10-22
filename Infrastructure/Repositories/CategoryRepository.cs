using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class CategoryRepository : Repo<DatabaseContext, CategoryEntity>
{
	public CategoryRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
	{

	}


}
