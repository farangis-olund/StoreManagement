using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using System.Collections.ObjectModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class PermissionsViewModel : ObservableObject
{
    private readonly PermissionRepository _permissionRepo;

    // Collection bound to DataGrid
    [ObservableProperty]
    private ObservableCollection<PermissionDto> permissions = new();

    [ObservableProperty]
    private PermissionDto? selectedPermission;

    public PermissionsViewModel(PermissionRepository permissionRepo)
    {
        _permissionRepo = permissionRepo;
        _ = LoadPermissionsAsync();
    }

    private async Task LoadPermissionsAsync()
    {
        Permissions.Clear();

        var list = (await _permissionRepo.GetAllAsync())
            .OrderBy(p => p.Key)       // 🔥 sort here
            .ToList();

        foreach (var p in list)
        {
            Permissions.Add(new PermissionDto
            {
                Id = p.Id,
                Key = p.Key,
                Description = p.Description,
                IsEditing = false
            });
        }
    }




    [RelayCommand]
    private void AddPermission()
    {
        // Create new UI DTO
        var newPermission = new PermissionDto
        {
            Id = 0,               // new item, not saved yet
            Key = "",
            Description = "",
            IsEditing = true
        };

        // Turn off editing on all others
        foreach (var p in Permissions)
            p.IsEditing = false;

        Permissions.Add(newPermission);

        // Select the new row for editing
        SelectedPermission = newPermission;   // if you track selection
    }



    [RelayCommand]
    private void DeletePermission(PermissionDto? permission)
    {
        if (permission == null)
            return;

        if (MessageBox.Show(
            $"Удалить право '{permission.Key}'?",
            "Подтверждение",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        ) == MessageBoxResult.Yes)
        {
            Permissions.Remove(permission);
        }
    }


    [RelayCommand]
    private void EditPermission(PermissionDto permission)
    {
        if (permission == null) return;

        // Turn off editing on all others
        foreach (var p in Permissions)
            p.IsEditing = false;

        permission.IsEditing = true;
        SelectedPermission = permission;
    }



    [RelayCommand]
    private async Task Save()
    {
        var list = Permissions
            .Select(p => new PermissionEntity
            {
                Id = p.Id,
                Key = p.Key,
                Description = p.Description
            })
            .ToList();

        await _permissionRepo.ReplaceAllAsync(list);

        // Reset editing state
        foreach (var p in Permissions)
            p.IsEditing = false;

        // 🔥 Reload from database so UI updates
        await LoadPermissionsAsync();
    }


}

public partial class PermissionDto : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty]
    private string key;

    [ObservableProperty]
    private string description;

    [ObservableProperty]
    private bool isEditing;
}
