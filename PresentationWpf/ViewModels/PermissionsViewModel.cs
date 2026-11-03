using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using System.Collections.ObjectModel;

namespace PresentationWpf.ViewModels;

public class PermissionsViewModel : ObservableObject
{
    private readonly PermissionRepository _permissionRepo;

    public ObservableCollection<PermissionEntity> Permissions { get; } = new();

    public PermissionsViewModel(PermissionRepository permissionRepo)
    {
        _permissionRepo = permissionRepo;
        _ = LoadPermissionsAsync();
    }

    private async Task LoadPermissionsAsync()
    {
        var permissions = await _permissionRepo.GetAllAsync();
        foreach (var permission in permissions.OrderBy(p => p.Key))
            Permissions.Add(permission);
    }
}
