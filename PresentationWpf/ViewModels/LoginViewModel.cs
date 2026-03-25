
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Services;
using System.Windows.Controls;

namespace PresentationWpf.ViewModels;

public partial class LoginViewModel(IServiceProvider serviceProvider,  UserSessionService userSessionService, PermissionService permissionService,
    RolePermissionRepository rolePermRepo,
    UserService userService) : ObservableObject
{
	private readonly IServiceProvider _serviceProvider = serviceProvider;
	private readonly UserSessionService _userSessionService = userSessionService;
    private readonly PermissionService _permissionService = permissionService;
    private readonly RolePermissionRepository _rolePermRepo = rolePermRepo;
   private readonly UserService _userService = userService;
	
    [ObservableProperty] private string _username = null!;
	[ObservableProperty] private string _message = null!;

    [RelayCommand]
    private async Task Login(object parameter)
    {
        // First-run check: no users -> open setup screen
        if (await NoUsersAsync())
        {
            var setupVm = _serviceProvider.GetRequiredService<CreateAdminViewModel>();
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            mainVm.CurrentViewModel = setupVm;
            return;
        }

        var passwordBox = parameter as PasswordBox;
        var password = passwordBox?.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            Message = "Вы не указали логин или пароль!";
            return;
        }

        // Try login first
        var success = await _userSessionService.LoginAsync(Username, password);

        if (!success)
        {
            Message = "Неправильно задан логин или пароль!";
            return;
        }

        // =============================================
        // 1) Load full user entity (with roles)
        // =============================================
        var user = await _userService.GetByUsernameAsync(Username);
        if (user == null)
        {
            Message = "Ошибка загрузки пользователя!";
            return;
        }

        // =============================================
        // 2) Load all permissions for this user's roles
        // =============================================
        var permissionKeys = await _userService.GetPermissionKeysAsync(user.Id);

        // Store permissions globally for UI
        _permissionService.SetPermissions(permissionKeys);

        var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
        mainVM.RefreshPermissions();
       
       
        mainVM.IsLoggedIn = true;
        mainVM.CurrentViewModel = _serviceProvider.GetRequiredService<WelcomeViewModel>();
    }

    private async Task<bool> NoUsersAsync()
    {
        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        await using var db = await factory.CreateDbContextAsync();
        return !await db.Users.AnyAsync();
    }

}
