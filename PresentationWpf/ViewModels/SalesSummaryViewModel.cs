using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.Win32;
using PresentationWpf.Documents;
using System.Windows;
using QuestPDF.Fluent;
using PresentationWpf.Views;
using System.IO;
using PresentationWpf.Services;
using Infrastructure.Entities;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace PresentationWpf.ViewModels;

public partial class SalesSummaryViewModel : ObservableObject
{
    private readonly CustomerService _customerService;
    private readonly CustomerFinanceService _financeService;
    private readonly UserSessionService _userSession;
    private readonly ManagerService _managerService;
    public SalesSummaryViewModel(
        CustomerService customerService,
        CustomerFinanceService financeService, UserSessionService userSession, ManagerService managerService)
    {
        _customerService = customerService;
        _financeService = financeService;
        _userSession = userSession;
        _managerService = managerService;

        Clients = [];
        Sales = [];

        _ = InitializeAsync();
    }

    // ================= DATA =================

    [ObservableProperty]
    private ObservableCollection<Customer> clients;

    [ObservableProperty]
    private Customer? selectedClient;

    [ObservableProperty]
    private ObservableCollection<SalesManagerEntity> managers = new();

    [ObservableProperty]
    private SalesManagerEntity? selectedManager;

    [ObservableProperty]
    private ObservableCollection<SalesSummaryRow> sales;
    
    [ObservableProperty]
    private ObservableCollection<InactivesSummaryRowDto> inactiveClients = [];
 

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private DateTime periodFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime periodTo = DateTime.Today;

    [ObservableProperty]
    private bool usePeriod;

    // ================= INIT =================

    public async Task InitializeAsync()
    {
        var customers = await _customerService.GetAllCustomersAsync();
        var customersEntity = await _customerService.GetAllCustomersAsync();

        Clients = new ObservableCollection<Customer>(
            customersEntity.Select(e => (Customer)e)
        );

        // 🔹 Load managers
        var managerList = await _managerService.GetManagersAsync();

        Managers = new ObservableCollection<SalesManagerEntity>(managerList);
        await LoadAllAsync();
    }

    partial void OnSelectedClientChanged(Customer? value)
    {
        _ = LoadSingleAsync(value);
    }

    // ================= LOAD =================

    private async Task LoadAllAsync()
    {
        Sales.Clear();
        TotalAmount = 0;

        foreach (var customer in Clients)
        {
            var info = await _financeService.GetFinanceInfoAsync(customer.Id);
            var lastOrderDate = await _financeService.GetLastOrderDateAsync(customer.Id);

            Sales.Add(new SalesSummaryRow
            {
                ClientCode = customer.Id ?? "",
                ClientName = customer.FullName ?? "",
                Address = customer.Address ?? "",
                Phone = customer.MobilePhone ?? "",
                MaxDate = lastOrderDate,

                Sales = info.TotalSales,
                Returns = info.TotalReturns,
                Debt = info.CustomerDebt,
                Payments = info.PreviousPayments - info.CurrentPayment,
                Balance = info.Balance
            });
        }

        TotalAmount = Sales.Sum(x => x.Balance);
    }

    private async Task LoadSingleAsync(Customer? customer)
    {
        Sales.Clear();
        TotalAmount = 0;

        if (customer == null)
            return;

        var info = await _financeService.GetFinanceInfoAsync(customer.Id);
        var lastOrderDate = await _financeService.GetLastOrderDateAsync(customer.Id);
        Sales.Add(new SalesSummaryRow
        {
            ClientCode = customer.Id ?? "",
            ClientName = customer.FullName ?? "",
            Address = customer.Address ?? "",
            Phone = customer.MobilePhone ?? "",
            MaxDate = lastOrderDate,
            Sales = info.TotalSales,
            Returns = info.TotalReturns,
            Debt = info.CustomerDebt,
            Payments = info.PreviousPayments - info.CurrentPayment,
            Balance = info.Balance
        });

        TotalAmount = info.Balance;
    }

    // ================= COMMANDS =================

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        SelectedClient = null;
        await LoadAllAsync();
    }

    [RelayCommand]
    private void Print()
    {
        var orgName = _userSession.OrganizationDisplayName;

        if (Sales == null || Sales.Count == 0)
        {
            MessageBox.Show(
                "Нет данных для печати.",
                "Печать отчёта",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // 🔹 Build the PDF report
        var document = new SalesSummaryReportDocument(this, orgName);

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(
            folder,
            $"SalesSummaryPreview_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        document.GeneratePdf(filePath);

        // 🔹 Show report preview
        var preview = new DocumentPreviewView(filePath);

        var window = new Window
        {
            Title = "Предварительный просмотр — Итоги по продажам",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        window.ShowDialog();
    }

    [RelayCommand]
    private async Task ActSverkiAsync()
    {
        if (SelectedClient == null)
        {
            MessageBox.Show("Выберите клиента");
            return;
        }

        var info = await _financeService.GetFinanceInfoAsync(SelectedClient.Id);
        var lastOrderDate = await _financeService.GetLastOrderDateAsync(SelectedClient.Id);

        var doc = new ReconciliationReportDocument(
            SelectedClient,
            info,
            lastOrderDate ?? DateTime.Today,
            _userSession.OrganizationDisplayName
        );

        var folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder,
            $"ActSverki_{SelectedClient.Id}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");

        doc.GeneratePdf(path);

        new Window
        {
            Title = "Акт сверки",
            Content = new DocumentPreviewView(path),
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        }.ShowDialog();
    }

    [RelayCommand]
    private async Task InactiveReportAsync()
    {
        InactiveClients.Clear();
        TotalAmount = 0;

        foreach (var customer in Clients)
        {
            var info = await _financeService.GetFinanceInfoAsync(customer.Id);

            // Skip if no debt
            if (info.Balance <= 0)
                continue;

            DateTime? lastOrderDate;

            if (UsePeriod)
            {
                lastOrderDate = await _financeService
                    .GetLastOrderDateInPeriodAsync(
                        customer.Id,
                        PeriodFrom,
                        PeriodTo);
            }
            else
            {
                lastOrderDate = await _financeService
                    .GetLastOrderDateAsync(customer.Id);
            }
                        

            // If customer NEVER had orders (no contracts)
            if (lastOrderDate != null)
                continue;

            InactiveClients.Add(new InactivesSummaryRowDto
            {
                ClientCode = customer.Id ?? string.Empty,
                ClientName = customer.FullName ?? string.Empty,
                Address = customer.Address ?? string.Empty,
                Phone = customer.MobilePhone ?? string.Empty,
                MaxDate = null,
                Balance = info.Balance,
                Restriction = customer.Restriction ?? 0
            });
        }

        TotalAmount = Sales.Sum(x => x.Balance);
    }
    
    [RelayCommand]
    private async Task LoadClientsByManagerAsync()
    {
        if (SelectedManager == null)
            return;

        InactiveClients.Clear();

        List<InactivesSummaryRowDto> result;

        if (UsePeriod)
        {
            result = await _financeService
                .GetClientsByManagerAndPeriodAsync(
                    SelectedManager.Id,
                    PeriodFrom,
                    PeriodTo);
        }
        else
        {
            result = await _financeService
                .GetClientsByManagerAsync(SelectedManager.Id);
        }

        foreach (var item in result)
            InactiveClients.Add(item);
    }

    [RelayCommand]
    private void PrintInactives()
    {
        var orgName = _userSession.OrganizationDisplayName;

        if (InactiveClients == null || InactiveClients.Count == 0)
        {
            MessageBox.Show(
                "Нет данных для печати.",
                "Печать отчёта",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var document = new InactivesReportDocument(this, orgName);

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(
            folder,
            $"Debtors_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        document.GeneratePdf(filePath);

        var preview = new DocumentPreviewView(filePath);

        var window = new Window
        {
            Title = "Предварительный просмотр — Список должников",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        window.ShowDialog();
    }

    [RelayCommand]
    private void PrintDetailInfo()
    {
        var orgName = _userSession.OrganizationDisplayName ?? string.Empty;

        if (Sales == null || Sales.Count == 0)
        {
            MessageBox.Show(
                "Нет данных для печати.",
                "Печать отчёта",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            // 🔹 Create document
            var document = new SalesDetailInfoReportDocument(
                Sales.ToList(),
                orgName               
            );

            // 🔹 Temp folder
            string folder = Path.Combine(Path.GetTempPath(), "Reports");
            Directory.CreateDirectory(folder);

            string filePath = Path.Combine(
                folder,
                $"DetailInfo_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            // 🔹 Generate PDF
            document.GeneratePdf(filePath);

            // 🔹 Show preview
            var preview = new DocumentPreviewView(filePath);

            var window = new Window
            {
                Title = "Предварительный просмотр — Детальная информация",
                Content = preview,
                Width = 1100,
                Height = 900,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка при создании отчёта:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void PrintAllInactives()
    {
        if (InactiveClients == null || InactiveClients.Count == 0)
        {
            MessageBox.Show(
                "Нет данных для печати.",
                "Печать",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var document = new AllInactivesReportDocument(
            InactiveClients.ToList(),
            _userSession.OrganizationDisplayName,
            UsePeriod,
            PeriodFrom,
            PeriodTo
        );

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(
            folder,
            $"AllInactives_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        document.GeneratePdf(filePath);

        new Window
        {
            Title = "Предварительный просмотр — Задолженность без оборота",
            Content = new DocumentPreviewView(filePath),
            Width = 1000,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        }.ShowDialog();
    }

}

public sealed class SalesSummaryRow
{
    public string ClientCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime? MaxDate { get; set; }
    public decimal Balance { get; set; }
    public decimal Sales { get; set; }          // Продажа
    public decimal Returns { get; set; }        // Возврат
    public decimal Debt { get; set; }           // Задолженность
    public decimal Payments { get; set; }       // Платежи

}

