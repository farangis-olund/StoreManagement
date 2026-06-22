using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Enums;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.Views;
using System.Collections.ObjectModel;
using System.Windows;
using Infrastructure.Helpers;
using QuestPDF.Fluent;
namespace PresentationWpf.ViewModels;

public partial class ExpenseCrudViewModel : ObservableObject
{
    private readonly ExpenseService _service;
    private readonly UserSessionService _userSessionService;
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;

    public PermissionService PermissionService { get; }

    public ExpenseCrudViewModel(
        ExpenseService service, UserSessionService userSessionService,
        IDbContextFactory<DatabaseContext> dbFactory, PermissionService permissionService)
    {
        _service = service;
        _dbFactory = dbFactory;
        _userSessionService = userSessionService;

        FromDate = null;
        ToDate = null;

        Reasons = ExpenseReasonMapper.GetItems(); // RU list
      
        PermissionService = permissionService;

        _ = LoadAsync();
    }

    // =========================
    // DATA
    // =========================
    public ObservableCollection<ExpenseDto> Rows { get; } = new();

    [ObservableProperty] private ExpenseDto? selectedRow;

    // =========================
    // EDIT FIELDS
    // =========================
    [ObservableProperty] private DateTime date = DateTime.Today;
    [ObservableProperty] int? userId;
    [ObservableProperty] private string? courierId;
    [ObservableProperty] private decimal amountEuro;
    [ObservableProperty] private decimal amountTjs;
    [ObservableProperty] private ReasonItem? selectedReason;
    [ObservableProperty] private string? note;

    public ObservableCollection<ExpensePersonItem> Persons { get; } = new();

    [ObservableProperty]
    private ExpensePersonItem? selectedPerson;

    public List<ReasonItem> Reasons { get; }

    // =========================
    // FILTERS
    // =========================
    [ObservableProperty] private DateTime? fromDate;
    [ObservableProperty] private DateTime? toDate;

    public bool CanViewRemove => PermissionService.Has("Expenses.Remove");
    public bool CanShowAllExpenses => PermissionService.Has("Expenses.ShowAllExpenses");
    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanViewRemove));
        OnPropertyChanged(nameof(CanShowAllExpenses));

    }


    [ObservableProperty] private decimal totalAmountTjs;
    [ObservableProperty] private decimal totalAmountEuro;
    [ObservableProperty] private int totalRowsCount;

    private void RecalculateTotals()
    {
        TotalAmountTjs = Rows.Sum(x => x.AmountTjs);
        TotalAmountEuro = Rows.Sum(x => x.AmountEuro);
        TotalRowsCount = Rows.Count;
    }

    
    // LOAD
       public async Task LoadAsync()
    {
        Rows.Clear();
        Persons.Clear();

        bool canShowAllExpenses = PermissionService.Has("Expenses.ShowAllExpenses");
        int currentUserId = _userSessionService.UserId;

        DateTime? from = FromDate;
        DateTime? to = ToDate;

        if (!canShowAllExpenses)
        {
            from = DateTime.Today;
            to = DateTime.Today;
        }

        var data = await _service.GetExpensesAsync(from, to);

        if (!canShowAllExpenses)
        {
            data = data
                .Where(x => x.UserId == currentUserId && x.Date.Date == DateTime.Today)
                .ToList();
        }

        foreach (var x in data)
            Rows.Add(x);

        RecalculateTotals();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var users = await db.Users
            .Select(x => new ExpensePersonItem
            {
                Type = "User",
                Id = x.Id.ToString(),
                Display = $"[Пользователь] - {x.FullName}"
            })
            .ToListAsync();

        var couriers = await db.Couriers
            .Select(x => new ExpensePersonItem
            {
                Type = "Courier",
                Id = x.Id.ToString(),
                Display = $"[Доставщик] - {x.FullName}"
            })
            .ToListAsync();

        foreach (var u in users)
            Persons.Add(u);

        foreach (var c in couriers)
            Persons.Add(c);
    }

    private bool _isUpdatingPeriod;

    partial void OnFromDateChanged(DateTime? v)
    {
        if (_isUpdatingPeriod) return;
        _ = LoadAsync();
    }

    partial void OnToDateChanged(DateTime? v)
    {
        if (_isUpdatingPeriod) return;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task ClearPeriod()
    {
        _isUpdatingPeriod = true;

        FromDate = null;
        ToDate = null;

        _isUpdatingPeriod = false;

        await LoadAsync();
    }
    public bool CanAdd => SelectedRow != null;

    // =========================
    // SELECT → EDIT
    // =========================
    partial void OnSelectedRowChanged(ExpenseDto? value)
    {

        OnPropertyChanged(nameof(IsNewMode));
        OnPropertyChanged(nameof(CanAdd));

        if (value == null) return;

        Date = value.Date;
        UserId = value.UserId;
        CourierId = value.CourierId;
        AmountEuro = value.AmountEuro;
        AmountTjs = value.AmountTjs;
        Note = value.Note;

        SelectedReason = Reasons.FirstOrDefault(r => r.Value.ToString() == value.Reason);

        if (value.UserId.HasValue)
        {
            SelectedPerson = Persons.FirstOrDefault(x =>
                x.Type == "User" && x.Id == value.UserId.Value.ToString());
        }
        else if (!string.IsNullOrWhiteSpace(value.CourierId))
        {
            SelectedPerson = Persons.FirstOrDefault(x =>
                x.Type == "Courier" && x.Id == value.CourierId);
        }
        else
        {
            SelectedPerson = null;
        }
    }

    partial void OnAmountTjsChanged(decimal value)
    {
        var rate = _userSessionService.ExchangeRate;

        if (rate > 0)
            AmountEuro = Math.Round(value / (decimal)rate, 2);
        else
            AmountEuro = 0;
    }
    [RelayCommand]
    private void Add()
    {
        ClearForm();
    }

    public bool IsNewMode => SelectedRow == null;

    // =========================
    // CREATE / UPDATE
    // =========================
    [RelayCommand]
    private async Task Save()
    {
        if (!ValidateBeforeSave())
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var rate = _userSessionService.ExchangeRate;
        var calculatedEuro = rate > 0 ? Math.Round(AmountTjs / (decimal)rate, 2) : 0;

        int selectedUserId = _userSessionService.UserId;
       
        ExpenseEntity? entity;
        bool isNew = SelectedRow == null;

        if (isNew)
        {
            entity = new ExpenseEntity
            {
                Date = Date,
                UserId = selectedUserId,
                AmountEuro = calculatedEuro,
                AmountSmn = AmountTjs,
                Reason = SelectedReason!.Value,
                Note = Note
            };

            db.Add(entity);
        }
        else
        {
            entity = await db.Set<ExpenseEntity>()
                .FirstOrDefaultAsync(x => x.Id == SelectedRow!.Id);

            if (entity == null) return;

            entity.Date = Date;
            entity.UserId = selectedUserId;
            entity.AmountEuro = calculatedEuro;
            entity.AmountSmn = AmountTjs;
            entity.Reason = SelectedReason!.Value;
            entity.Note = Note;
        }

        await db.SaveChangesAsync();

        var savedId = entity.Id;

        await LoadAsync();
        ClearForm();

        await ShowExpenseInvoiceAsync(savedId);
    }

    // =========================
    // DELETE
    // =========================
    [RelayCommand]
  
    private async Task Delete()
    {
        if (SelectedRow == null)
        {
            MessageBox.Show(
                "Выберите расход для удаления.",
                "Удаление",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var confirm = MessageBox.Show(
             $"Удалить расход № {SelectedRow.Id}?",
             "Подтверждение удаления",
             MessageBoxButton.YesNo,
             MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var entity = await db.Set<ExpenseEntity>()
            .FirstOrDefaultAsync(x => x.Id == SelectedRow.Id);

        if (entity == null)
        {
            MessageBox.Show(
                "Расход не найден.",
                "Удаление",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        db.Remove(entity);
        await db.SaveChangesAsync();

        MessageBox.Show(
            "Расход успешно удалён.",
            "Удаление",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        await LoadAsync();
        ClearForm();
    }

    // =========================
    // PRINT
    // =========================
    [RelayCommand]
    private async Task Print()
    {
        if (SelectedRow == null)
        {
            MessageBox.Show("Выберите расход.", "Печать");
            return;
        }

        await ShowExpenseInvoiceAsync(SelectedRow.Id);
    }


    private async Task ShowExpenseInvoiceAsync(int expenseId)
    {
        
        await using var db = await _dbFactory.CreateDbContextAsync();

        var expense = await db.Set<ExpenseEntity>()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Courier)
            .FirstOrDefaultAsync(x => x.Id == expenseId);

        if (expense == null)
        {
            MessageBox.Show("Расход не найден.", "Печать");
            return;
        }

        string? personName = null;
        string? personType = null;
        string? personCode = null;

        if (expense.User != null)
        {
            personType = "Пользователь";
            personCode = expense.User.Id.ToString();
            personName = $"{expense.User.FirstName} {expense.User.LastName}".Trim();
        }
        else if (expense.Courier != null)
        {
            personType = "Доставщик";
            personCode = expense.Courier.Id.ToString();
            personName = expense.Courier.FullName; // or adjust to your courier fields
        }
        else if (!string.IsNullOrWhiteSpace(expense.CourierId))
        {
            personType = "Доставщик";
            personCode = expense.CourierId;
            personName = expense.CourierId;
        }

        var vm = new ExpenseInvoiceViewModel
        {
            Id = expense.Id,
            Date = expense.Date,
            PersonName = personName,
            PersonType = personType,
            PersonCode = personCode,
            Reason = expense.Reason.ToRussian(),
            Note = expense.Note,
            AmountTjs = expense.AmountSmn,
            AmountEuro = expense.AmountEuro,
            Rate = (decimal)_userSessionService.ExchangeRate,
            ShopName = _userSessionService.OrganizationDisplayName
        };

        var document = new ExpenseInvoiceDocument(vm);
        var pdfBytes = document.GeneratePdf();

        var preview = new DocumentPreviewView();
        preview.LoadPdf(pdfBytes);

        var window = new Window
        {
            Title = $"Просмотр расхода № {expense.Id}",
            Content = preview,
            Width = 900,
            Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        window.ShowDialog();
    }

    // =========================
    // HELPERS
    // =========================
    private void ClearForm()
    {
        SelectedRow = null;
        Date = DateTime.Today;
        UserId = null;
        CourierId = null;
        SelectedPerson = null;
        AmountEuro = 0;
        AmountTjs = 0;
        Note = null;
        SelectedReason = null;
    }

    private bool ValidateBeforeSave()
    {
        
        if (AmountTjs <= 0)
        {
            MessageBox.Show("Введите сумму больше нуля.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (SelectedReason == null)
        {
            MessageBox.Show("Выберите причину расхода.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }
}

public class ExpensePersonItem
{
    public string Type { get; set; } = "";   // "User" or "Courier"
    public string Id { get; set; } = "";
    public string Display { get; set; } = "";
}

public class ExpenseInvoiceViewModel
{
    public int Id { get; set; }
    public DateTime Date { get; set; }

    public string? PersonName { get; set; }
    public string? PersonType { get; set; }   // Пользователь / Доставщик
    public string? PersonCode { get; set; }

    public string? Reason { get; set; }
    public string? Note { get; set; }

    public decimal AmountTjs { get; set; }
    public decimal AmountEuro { get; set; }
    public decimal Rate { get; set; }

    public string ShopName { get; set; } = "AutoSpaire Store";
    public string InvoiceNumber => $"РАСХ-{Id}";
}