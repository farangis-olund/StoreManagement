
using Infrastructure.Entities;
using Infrastructure.Repositories;

using System.Diagnostics;

namespace Infrastructure.Services;

public class GroupService
{
    private readonly GroupRepository _groupRepository;


    public GroupService(GroupRepository GroupRepository)
    {
        _groupRepository = GroupRepository;
       
    }

    public async Task<GroupEntity?> AddGroupAsync(string groupName)
{
    try
    {
        var existingGroup = await _groupRepository.GetOneAsync(g => g.GroupName == groupName);

        // ✅ Only add if it doesn't exist or name is null/empty
        if (existingGroup == null || string.IsNullOrEmpty(existingGroup.GroupName))
        {
            var newGroup = new GroupEntity { GroupName = groupName };
            await _groupRepository.AddAsync(newGroup);
            return newGroup;
        }

        return existingGroup;
    }
    catch (Exception ex)
    {

        Debug.WriteLine($"Error getting/adding group: {ex.Message}");
        return null;
    }
}


    public async Task<GroupEntity> GetGroupAsync(string GroupName)
    {
        try
        {
            return await _groupRepository.GetOneAsync(b => b.GroupName == GroupName);
        }
        catch (Exception ex)
        {

            Debug.WriteLine($"Error getting/adding Group: {ex.Message}");
            return null!;
        }
    }


    public async Task<IEnumerable<GroupEntity>> GetAllCategoriesAsync()
    {
        try
        {
            return await _groupRepository.GetAllAsync() ?? Enumerable.Empty<GroupEntity>();
        }
        catch (Exception ex)
        {

            Debug.WriteLine($"Error getting Groups: {ex.Message}");
            return Enumerable.Empty<GroupEntity>();
        }
    }


    public async Task<GroupEntity> UpdateGroupAsync(GroupEntity Group)
    {
        try
        {
           return await _groupRepository.UpdateAsync(c => c.Id == Group.Id, Group);
        }
        catch (Exception ex)
        {

            Debug.WriteLine($"Error in updating product: {ex.Message}");
            return null!;
        }
    }

    public async Task<bool> DeleteGroupAsync(string GroupName)
    {
        try
        {
            var result = await _groupRepository.GetOneAsync(b => b.GroupName == GroupName);
            if (result != null)
            {
                await _groupRepository.RemoveAsync(b => b.GroupName == GroupName);
                return true;
            } else
                return false;
        }
        catch (Exception ex)
        {

            Debug.WriteLine($"Error deleting Group: {ex.Message}");
            return false;
        }
    }
}
