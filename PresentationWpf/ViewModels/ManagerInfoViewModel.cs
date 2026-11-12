using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class ManagerInfoViewModel(ManagerService managerService) : ObservableObject
{
    [ObservableProperty] private ObservableCollection<SalesManagerEntity> managers = [];
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try { Managers = new(await managerService.GetManagersAsync()); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Менеджеры", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    public Task LoadAsync() => RefreshAsync();
}
