using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PresentationWpf.ViewModels
{
    public partial class UserViewModel : ObservableObject
    {
        private readonly UserService _userService;

        [ObservableProperty]
        private ObservableCollection<UserEntity> users = new();

        [ObservableProperty]
        private ObservableCollection<RoleEntity> roles = new();

        [ObservableProperty]
        private UserEntity currentUser = new();

        [ObservableProperty]
        private RoleEntity selectedRole = new();

        public UserViewModel(UserService userService)
        {
            _userService = userService;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var userList = await _userService.GetAllAsync();
            Users = new ObservableCollection<UserEntity>(userList);

            var roleList = await _userService.GetRolesAsync();
            Roles = new ObservableCollection<RoleEntity>(roleList);
        }

        partial void OnCurrentUserChanged(UserEntity value)
        {
            if (value?.UserRoles != null && value.UserRoles.Any())
            {
                var userRole = value.UserRoles.FirstOrDefault()?.Role;
                if (userRole != null)
                {
                    SelectedRole = Roles.FirstOrDefault(r => r.Id == userRole.Id);
                }
            }
            else
            {
                SelectedRole = null;
            }
        }

        [RelayCommand]
        private async Task AddUserAsync()
        {
            if (CurrentUser != null && SelectedRole != null)
            {
                await _userService.AddAsync(CurrentUser, SelectedRole.Id);
                await LoadDataAsync();
                CurrentUser = new UserEntity();
            }
        }

        [RelayCommand]
        private async Task UpdateUserAsync()
        {
            if (CurrentUser != null)
            {
                await _userService.UpdateAsync(CurrentUser, SelectedRole?.Id);
                await LoadDataAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteUserAsync()
        {
            if (CurrentUser != null)
            {
                await _userService.DeleteAsync(CurrentUser.Id);
                await LoadDataAsync();
                CurrentUser = new UserEntity();
            }
        }
    }
}
