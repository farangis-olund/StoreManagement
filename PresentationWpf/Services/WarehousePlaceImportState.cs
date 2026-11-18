using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Dtos;
using System.Windows;

namespace PresentationWpf.Services;

public sealed record WarehousePlaceImportSnapshot(string FileName, DateTime ImportedAt, int RowCount,
    int UpdatedCount, int DuplicateCount, IReadOnlyList<WarehousePlaceImportIssue> Issues)
{
    public IReadOnlyList<Product> UpdatedProducts { get; init; } = [];
    public int MissingCount => Issues.Count(i => i.Kind == WarehousePlaceImportIssueKind.ArticleNotFound);
    public string Summary => $"Обработано строк: {RowCount}. Обновлено мест: {UpdatedCount}. " +
        $"Не найдено артикулов в базе: {MissingCount}. Других ошибок: {Issues.Count - MissingCount}." +
        (DuplicateCount > 0 ? $" Повторов: {DuplicateCount}; использована последняя строка артикула." : "");
}

// Session-only state survives reopening Inventory and stays separate from movement-import errors.
public partial class WarehousePlaceImportState : ObservableObject
{
    private CancellationTokenSource? _cancellation;
    [ObservableProperty] private WarehousePlaceImportSnapshot? snapshot;
    [ObservableProperty] private bool isImporting;

    public WarehousePlaceImportState(UserSessionService session) =>
        WeakEventManager<UserSessionService, EventArgs>.AddHandler(session, nameof(UserSessionService.UserChanged), UserChanged);

    public CancellationToken BeginImport()
    {
        if (IsImporting) throw new InvalidOperationException("Импорт уже выполняется.");
        _cancellation = new();
        IsImporting = true;
        return _cancellation.Token;
    }

    public void EndImport()
    {
        _cancellation?.Dispose();
        _cancellation = null;
        IsImporting = false;
    }

    private void UserChanged(object? sender, EventArgs e)
    {
        _cancellation?.Cancel();
        Snapshot = null;
    }
}
