using CommunityToolkit.Mvvm.ComponentModel;

namespace PresentationWpf.Dtos;

public partial class RoleDto : ObservableObject
{
    [ObservableProperty] private int id;
    [ObservableProperty] private string roleName = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private bool isEditing;
}
