using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class WarehousePlaceImportErrorsViewModel : ObservableObject
{
    private readonly WarehousePlaceImportSnapshot _snapshot;
    private readonly string _shop;
    private readonly PermissionService _permissions;
    public IReadOnlyList<WarehousePlaceImportIssue> Issues => CanPrint ? _snapshot.Issues : [];
    public string FileDescription => $"{_snapshot.FileName} — {_snapshot.ImportedAt:dd.MM.yyyy HH:mm}";
    public string Summary => _snapshot.Summary;
    public bool CanPrint => _snapshot.Issues.Count > 0 && _permissions.Has(PermissionCatalog.Inventory);
    [ObservableProperty] private string message = "";

    public WarehousePlaceImportErrorsViewModel(WarehousePlaceImportSnapshot snapshot, string shop, PermissionService permissions)
    {
        _snapshot = snapshot; _shop = shop; _permissions = permissions;
        PropertyChangedEventManager.AddHandler(permissions, PermissionsChanged, nameof(PermissionService.CurrentPermissions));
    }
    private void PermissionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Issues)); PrintCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        if (!CanPrint) return;
        try { PrintReport(); }
        catch (Exception ex) { Message = $"Не удалось подготовить печать: {ex.GetBaseException().Message}"; }
    }
    protected virtual void PrintReport()
    {
        var folder = Path.Combine(Path.GetTempPath(), "WarehousePlaceImports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"ImportErrors_{Guid.NewGuid():N}.pdf");
        new WarehousePlaceImportErrorsDocument(_snapshot, _shop).GeneratePdf(path);
        new Window { Title = "Ошибки импорта мест на складе", Content = new DocumentPreviewView(path),
            Width = 900, Height = 950, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.Windows.OfType<WarehousePlaceImportErrorsWindow>().FirstOrDefault(w => w.IsActive) }.ShowDialog();
    }
}
