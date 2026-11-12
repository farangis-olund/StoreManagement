using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

// A detached, observable draft keeps unsaved edits out of the user list and database context.
public partial class UserEditModel : ObservableObject
{
    public int Id { get; init; }
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string firstName = "";
    [ObservableProperty] private string lastName = "";
    [ObservableProperty] private string? email;
    [ObservableProperty] private string? phoneNumber;
    [ObservableProperty] private string? address;
    [ObservableProperty] private string password = "";
    [ObservableProperty] private bool isManager;

    public UserEntity ToEntity() => new() { Id = Id, UserName = UserName, FirstName = FirstName,
        LastName = LastName, Email = Email, PhoneNumber = PhoneNumber, Address = Address,
        Password = Password, IsManager = IsManager };
}

public partial class UserViewModel : ObservableObject
{
    private readonly UserService _userService;
    private readonly UserSessionService _userSession;
    private readonly SessionPermissionRefresher _permissionRefresher;
    [ObservableProperty] private ObservableCollection<UserEntity> users = [];
    [ObservableProperty] private ObservableCollection<RoleEntity> roles = [];
    [ObservableProperty] private UserEntity? selectedUser;
    [ObservableProperty] private UserEditModel currentUser = new();
    [ObservableProperty] private RoleEntity? selectedRole;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool isBusy;
    public bool IsNotBusy => !IsBusy;
    public bool IsEditMode => CurrentUser.Id > 0;
    public bool IsUserValid => !string.IsNullOrWhiteSpace(CurrentUser.UserName) &&
        !string.IsNullOrWhiteSpace(CurrentUser.FirstName) && !string.IsNullOrWhiteSpace(CurrentUser.LastName) &&
        !string.IsNullOrWhiteSpace(CurrentUser.Password) && SelectedRole != null;

    public UserViewModel(UserService userService, UserSessionService userSession, SessionPermissionRefresher permissionRefresher)
    {
        _userService = userService; _userSession = userSession; _permissionRefresher = permissionRefresher;
        CurrentUser.PropertyChanged += DraftChanged;
        _ = LoadInitialAsync();
    }

    private async Task LoadInitialAsync()
    {
        IsBusy = true;
        try { await LoadDataAsync(); ResetDraft(null); }
        catch (Exception ex) { ShowError(ex); }
        finally { IsBusy = false; }
    }

    private async Task LoadDataAsync()
    {
        var list = await _userService.GetAllAsync();
        var roleList = await _userService.GetRolesAsync();
        Users = new(list); Roles = new(roleList);
    }

    partial void OnSelectedUserChanged(UserEntity? value) => ResetDraft(value);
    private void ResetDraft(UserEntity? value)
    {
        CurrentUser = value == null ? new() : new() { Id = value.Id, UserName = value.UserName,
            FirstName = value.FirstName, LastName = value.LastName, Email = value.Email,
            PhoneNumber = value.PhoneNumber, Address = value.Address, Password = value.Password, IsManager = value.IsManager };
        SelectedRole = Roles.FirstOrDefault(r => r.Id == value?.UserRoles.FirstOrDefault()?.RoleId);
        NotifyCommands();
    }
    partial void OnCurrentUserChanging(UserEditModel value) => CurrentUser.PropertyChanged -= DraftChanged;
    partial void OnCurrentUserChanged(UserEditModel value) { value.PropertyChanged += DraftChanged; NotifyCommands(); }
    partial void OnSelectedRoleChanged(RoleEntity? value) => NotifyCommands();
    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    private void DraftChanged(object? sender, PropertyChangedEventArgs e) => NotifyCommands();
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(IsUserValid)); OnPropertyChanged(nameof(IsEditMode));
        AddNewUserCommand.NotifyCanExecuteChanged(); AddUserCommand.NotifyCanExecuteChanged();
        UpdateUserCommand.NotifyCanExecuteChanged(); DeleteUserCommand.NotifyCanExecuteChanged();
    }
    private bool CanAddUser() => !IsBusy && !IsEditMode && IsUserValid;
    private bool CanUpdateUser() => !IsBusy && IsEditMode && IsUserValid;
    private bool CanDeleteUser() => !IsBusy && IsEditMode;
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private void AddNewUser() { SelectedUser = null; ResetDraft(null); }
    [RelayCommand(CanExecute = nameof(CanAddUser))]
    private Task AddUserAsync() => SaveAsync(true);
    [RelayCommand(CanExecute = nameof(CanUpdateUser))]
    private Task UpdateUserAsync() => SaveAsync(false);
    private async Task SaveAsync(bool create)
    {
        if (!IsUserValid || IsBusy) return;
        var input = CurrentUser.ToEntity();
        var roleId = SelectedRole!.Id;
        IsBusy = true;
        try
        {
            if (create) await _userService.AddAsync(input, roleId);
            else await _userService.UpdateAsync(input, roleId);
            _userSession.UpdateCurrentUser(input);
            await RefreshSessionPermissionsAsync();
            await LoadDataAsync();
            SelectedUser = Users.FirstOrDefault(u => u.Id == input.Id);
            MessageBox.Show("Пользователь и данные менеджера сохранены.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { IsBusy = false; }
    }
    [RelayCommand(CanExecute = nameof(CanDeleteUser))]
    private async Task DeleteUserAsync()
    {
        if (!CanDeleteUser()) return;
        var id = CurrentUser.Id;
        if (MessageBox.Show($"Удалить пользователя «{CurrentUser.UserName}»?", "Пользователи",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await _userService.DeleteAsync(id);
            await RefreshSessionPermissionsAsync();
            await LoadDataAsync(); SelectedUser = null; ResetDraft(null);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { IsBusy = false; }
    }
    private async Task RefreshSessionPermissionsAsync()
    {
        try { await _permissionRefresher.RefreshAsync(); }
        catch (Exception) { MessageBox.Show("Данные сохранены, но не удалось обновить права текущего пользователя. Проверьте подключение к базе данных.",
            "Права доступа", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private static void ShowError(Exception ex) => MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Warning);
}
