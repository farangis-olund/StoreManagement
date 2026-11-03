using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace PresentationWpf.ViewModels;

public partial class RoleManagementViewModel : ObservableObject
{
    private readonly RoleRepository _roleRepo;
    private readonly PermissionRepository _permRepo;
    private readonly RolePermissionRepository _rolePermRepo;
    private readonly SessionPermissionRefresher _permissionRefresher;

    public ObservableCollection<RoleDto> Roles { get; } = new();

    [ObservableProperty]
    private ObservableCollection<PermissionItem> permissions = new();

    public ICollectionView FilteredPermissions => CollectionViewSource.GetDefaultView(Permissions);
    public bool HasVisiblePermissions => !FilteredPermissions.IsEmpty;

    [ObservableProperty]
    private string permissionSearchText = string.Empty;

    partial void OnPermissionsChanged(ObservableCollection<PermissionItem> value)
    {
        FilteredPermissions.Filter = MatchesPermissionSearch;
        OnPropertyChanged(nameof(FilteredPermissions));
        OnPropertyChanged(nameof(HasVisiblePermissions));
        RefreshSelectAllState();
    }

    partial void OnPermissionSearchTextChanged(string value)
    {
        FilteredPermissions.Refresh();
        OnPropertyChanged(nameof(HasVisiblePermissions));
        RefreshSelectAllState();
        ClearPermissionSearchCommand.NotifyCanExecuteChanged();
    }

    private bool MatchesPermissionSearch(object item)
    {
        if (item is not PermissionItem permission) return false;
        var terms = (PermissionSearchText ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return terms.All(term => permission.Key.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (permission.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    [RelayCommand(CanExecute = nameof(CanClearPermissionSearch))]
    private void ClearPermissionSearch() => PermissionSearchText = string.Empty;
    private bool CanClearPermissionSearch() => !string.IsNullOrEmpty(PermissionSearchText);


    [ObservableProperty]
    private RoleDto selectedRole = null!;

    [ObservableProperty]
    private object? permissionManagementView;

    public RoleManagementViewModel(
    RoleRepository roleRepo,
    PermissionRepository permRepo,
    RolePermissionRepository rolePermRepo,
    PermissionsView permissionsView,          // Inject the view
    PermissionsViewModel permissionsVM,        // Inject the VM
    SessionPermissionRefresher permissionRefresher
)
    {
        _roleRepo = roleRepo;
        _permRepo = permRepo;
        _rolePermRepo = rolePermRepo;
        _permissionRefresher = permissionRefresher;
        FilteredPermissions.Filter = MatchesPermissionSearch;

        PermissionManagementView = permissionsView;
        permissionsView.DataContext = permissionsVM;

        _ = InitializeAsync();

        TogglePermissionManagerCommand = new RelayCommand(() =>
        {
            IsPermissionManagerVisible = !IsPermissionManagerVisible;
        });
    }

    public async Task InitializeAsync()
    {
        await LoadRoles();
    }

    private async Task LoadRoles()
    {
        Roles.Clear();

        var list = await _roleRepo.GetAllAsync();

        foreach (var r in list)
        {
            Roles.Add(new RoleDto
            {
                Id = r.Id,
                RoleName = r.RoleName,
                Description = r.Description,
                IsEditing = false
            });
        }
        RefreshSelectAllState();
    }


    partial void OnSelectedRoleChanged(RoleDto role)
    {
        if (role == null)
        {
            IsPermissionsVisible = false;
            return;
        }

        IsPermissionsVisible = true;

        // Run async safely
        async void Load() => await LoadPermissions(role.Id);
        Load();
    }



    [RelayCommand]
    private void AddRole()
    {
        var newRole = new RoleDto
        {
            RoleName = "Новая роль"
        };

        // Add temporary role to UI list
        Roles.Add(newRole);

        // Select it so user can edit immediately
        SelectedRole = newRole;
    }

   
    [RelayCommand]
    private async Task SaveRole()
    {
        if (SelectedRole == null) return;

        if (SelectedRole.Id == 0)
        {
            // new role
            await _roleRepo.AddAsync(new RoleEntity
            {
                RoleName = SelectedRole.RoleName,
                Description = SelectedRole.Description
            });
        }
        else
        {
            // update
            await _roleRepo.UpdateAsync(r => r.Id == SelectedRole.Id, new RoleEntity
            {
                RoleName = SelectedRole.RoleName,
                Description = SelectedRole.Description
            });
        }

        SelectedRole.IsEditing = false;

        await LoadRoles();
    }


    [RelayCommand]
    private async Task DeleteRole(RoleDto roleToDelete)
    {
        if (roleToDelete == null)
            return;

        var result = MessageBox.Show(
            $"Вы действительно хотите удалить роль \"{roleToDelete.RoleName}\"?",
            "Удаление роли",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        await _rolePermRepo.ClearPermissionsAsync(roleToDelete.Id);
        await _roleRepo.RemoveAsync(r => r.Id == roleToDelete.Id);
        await RefreshSessionPermissionsAsync();

        await LoadRoles();

        MessageBox.Show("Роль успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

        // Fix side effect: reset selection if deleted role was selected
        if (SelectedRole?.Id == roleToDelete.Id)
            SelectedRole = null;
    }




    [RelayCommand]
    private async Task SavePermissions()
    {
        if (SelectedRole == null) return;

        // collect selected permission IDs
        var assignedPermIds = Permissions
            .Where(p => p.IsAssigned)
            .Select(p => p.Id)
            .ToList();

        // remove old permissions
        await _rolePermRepo.ClearPermissionsAsync(SelectedRole.Id);

        // add new permissions
        await _rolePermRepo.AddPermissionsAsync(SelectedRole.Id, assignedPermIds);
        await RefreshSessionPermissionsAsync();

        MessageBox.Show(
            "Права сохранены!",
            "Успех",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    [RelayCommand]
    private void EditRole(RoleDto role)
    {
        if (role == null) return;

        // Stop other roles from editing
        foreach (var r in Roles)
            r.IsEditing = false;

        role.IsEditing = true;
        SelectedRole = role;
    }

    private async Task RefreshSessionPermissionsAsync()
    {
        try
        {
            await _permissionRefresher.RefreshAsync();
        }
        catch (Exception)
        {
            MessageBox.Show("Изменения сохранены, но не удалось обновить права текущего пользователя. Проверьте подключение к базе данных.",
                "Права доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool _isPermissionManagerVisible;
    public bool IsPermissionManagerVisible
    {
        get => _isPermissionManagerVisible;
        set => SetProperty(ref _isPermissionManagerVisible, value);
    }

    public ICommand TogglePermissionManagerCommand { get; }

    [ObservableProperty]
    private bool areAllSelected = false;

    partial void OnAreAllSelectedChanged(bool value)
    {
        if (_isLoadingPermissions) return; // ❗ PREVENT UNCHECKING ON LOAD
        if (_isApplyingSelectAll) return;  // ❗ PREVENT RECURSION

        _isApplyingSelectAll = true;
        try
        {
            foreach (var p in FilteredPermissions.Cast<PermissionItem>().ToList())
                p.IsAssigned = value;
        }
        finally
        {
            _isApplyingSelectAll = false;
            RefreshSelectAllState();
        }
    }



    public void RefreshSelectAllState()
    {
        if (_isLoadingPermissions) return; // ❗ DO NOT UPDATE DURING LOAD
        if (_isApplyingSelectAll) return;

        var visible = FilteredPermissions.Cast<PermissionItem>().ToList();
        bool newState = visible.Count > 0 && visible.All(p => p.IsAssigned);

        // Prevent calling OnAreAllSelectedChanged
        _isApplyingSelectAll = true;
        AreAllSelected = newState;
        _isApplyingSelectAll = false;
    }


    [ObservableProperty]
    private bool isPermissionsVisible = false;

    private bool _isLoadingPermissions = false;
        
    private bool _isApplyingSelectAll = false;


    private async Task LoadPermissions(int roleId)
    {
        _isLoadingPermissions = true;

        var allPerms = await _permRepo.GetAllAsync();
        var rolePermIds = await _rolePermRepo.GetPermissionIdsAsync(roleId);

        Permissions = new ObservableCollection<PermissionItem>(
        allPerms
        .OrderByDescending(p => rolePermIds.Contains(p.Id))  // Checked first
        .ThenBy(p => p.Description)                          // Then alphabetical
        .Select(p => new PermissionItem
        {
            Id = p.Id,
            Key = p.Key,
            Description = p.Description,
            IsAssigned = rolePermIds.Contains(p.Id),
            Parent = this
        })
);


        OnPropertyChanged(nameof(Permissions));

        _isLoadingPermissions = false;

        RefreshSelectAllState();
    }



}

public partial class PermissionItem : ObservableObject
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoleManagementViewModel Parent { get; set; }

    partial void OnIsAssignedChanged(bool value)
    {
        Parent?.RefreshSelectAllState();
    }

    [ObservableProperty]
    private bool isAssigned;
}
