
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;


namespace Infrastructure.Repositories;

public class GroupRepository : Repo<DatabaseContext, GroupEntity>
{
    public GroupRepository(IDbContextFactory<DatabaseContext> contextFactory) : base(contextFactory)
	{
    }

   
}
