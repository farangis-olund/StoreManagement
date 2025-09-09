
using Infrastructure.Contexts;
using Infrastructure.Entities;


namespace Infrastructure.Repositories;

public class GroupRepository : Repo<DatabaseContext, GroupEntity>
{
    public GroupRepository(DatabaseContext context) : base(context)
    {
    }

   
}
