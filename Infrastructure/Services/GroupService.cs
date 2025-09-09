
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Infrastructure.Services;

public class GroupService
{
    private readonly GroupRepository _groupRepository;
    private readonly ILogger<GroupService> _logger;

    public GroupService(GroupRepository GroupRepository, ILogger<GroupService> logger)
    {
        _groupRepository = GroupRepository;
        _logger = logger;
    }

    public async Task<GroupEntity> AddGroupAsync(string GroupName)
    {
        try
        {
            var existingGroup = await _groupRepository.GetOneAsync(b => b.GroupName == GroupName)
                                  ?? await _groupRepository.AddAsync(new GroupEntity { GroupName = GroupName });

            return existingGroup;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting/adding Group: {ex.Message}");
            Debug.WriteLine($"Error getting/adding Group: {ex.Message}");
            return null!;
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
            _logger.LogError($"Error getting/adding Group: {ex.Message}");
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
            _logger.LogError($"Error getting Groups: {ex.Message}");
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
            _logger.LogError($"Error in updating product: {ex.Message}");
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
            _logger.LogError($"Error deleting Group: {ex.Message}");
            Debug.WriteLine($"Error deleting Group: {ex.Message}");
            return false;
        }
    }
}
