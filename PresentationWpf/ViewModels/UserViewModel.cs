using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace PresentationWpf.ViewModels
{
    public partial class UserViewModel : ObservableObject
    {
        private readonly UserService _userService;
        private readonly UserSessionService _userSession;

        [ObservableProperty]
        private ObservableCollection<UserEntity> users = new();

        [ObservableProperty]
        private ObservableCollection<RoleEntity> roles = new();

        [ObservableProperty]
        private UserEntity currentUser = new();

        [ObservableProperty]
        private RoleEntity selectedRole = new();

        public UserViewModel(UserService userService, UserSessionService userSession)
        {
            _userService = userService;
            _userSession = userSession;

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
            // --- keep your existing logic ---
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

            // --- NEW: enable/disable Add/Update buttons based on validity ---
            OnPropertyChanged(nameof(IsUserValid));
            AddUserCommand?.NotifyCanExecuteChanged();
            UpdateUserCommand?.NotifyCanExecuteChanged();
            DeleteUserCommand?.NotifyCanExecuteChanged();


        }
        public bool IsEditMode => CurrentUser?.Id > 0;

        private bool CanAddUser() => !IsEditMode && IsUserValid;
        private bool CanUpdateUser() => IsEditMode;

        [RelayCommand]
        private void AddNewUser()
        {
            CurrentUser = new UserEntity();
            SelectedRole = null!;
        }

        [RelayCommand(CanExecute = nameof(CanAddUser))]
        private async Task AddUserAsync()
        {
            if (CurrentUser == null)
            {
                MessageBox.Show("Пользователь не заполнен.",
                    "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ---- VALIDATION ----

            if (string.IsNullOrWhiteSpace(CurrentUser.FirstName))
            {
                MessageBox.Show("Введите имя пользователя.",
                    "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentUser.LastName))
            {
                MessageBox.Show("Введите фамилию пользователя.",
                    "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentUser.UserName))
            {
                MessageBox.Show("Введите логин пользователя.",
                    "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentUser.Password))
            {
                MessageBox.Show("Введите пароль пользователя.",
                    "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedRole == null)
            {
                MessageBox.Show("Выберите роль для пользователя.",
                    "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if login already exists
            bool loginExists = await _userService.ExistsAsync(CurrentUser.UserName);
            if (loginExists)
            {
                MessageBox.Show("Пользователь с таким логином уже существует!",
                    "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            // ---- CREATE USER ----

            await _userService.AddAsync(CurrentUser, SelectedRole.Id);

            await LoadDataAsync();
            CurrentUser = new UserEntity();

            MessageBox.Show("Пользователь успешно добавлен.",
                "Добавление пользователя", MessageBoxButton.OK, MessageBoxImage.Information);
        }


       [RelayCommand(CanExecute = nameof(CanUpdateUser))]
        private async Task UpdateUserAsync()
        {
            if (CurrentUser == null)
            {
                MessageBox.Show("Выберите пользователя для редактирования.", "Изменение пользователя",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _userService.UpdateAsync(CurrentUser, SelectedRole?.Id);
            await LoadDataAsync();
            _userSession.UpdateCurrentUser(CurrentUser);
            MessageBox.Show("Изменения успешно сохранены.", "Изменение пользователя",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        
        [RelayCommand(CanExecute = nameof(CanUpdateUser))]
        private async Task DeleteUserAsync()
    {
        if (CurrentUser == null)
        {
            MessageBox.Show("Выберите пользователя для удаления.", "Удаление пользователя",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Вы действительно хотите удалить пользователя \"{CurrentUser.UserName}\"?",
            "Удаление пользователя",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        await _userService.DeleteAsync(CurrentUser.Id);
        await LoadDataAsync();
        CurrentUser = new UserEntity();

        MessageBox.Show("Пользователь успешно удалён.", "Удаление пользователя",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

        public bool IsUserValid =>
            CurrentUser != null &&
            !string.IsNullOrWhiteSpace(CurrentUser.UserName) &&
            !string.IsNullOrWhiteSpace(CurrentUser.FirstName) &&
            !string.IsNullOrWhiteSpace(CurrentUser.LastName) &&
            !string.IsNullOrWhiteSpace(CurrentUser.Password) &&
            SelectedRole != null;

       
        partial void OnSelectedRoleChanged(RoleEntity value)
        {
            OnPropertyChanged(nameof(IsUserValid));
            AddUserCommand.NotifyCanExecuteChanged();
        }

    }
}
