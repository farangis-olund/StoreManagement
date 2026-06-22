
using Infrastructure.Contexts;
using Infrastructure.Helpers;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;


namespace PresentationWpf;

public partial class App : Application
{
    private static IHost? builder;

    // --- TextBox ---
    private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var tb = (TextBox)sender;
        tb.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(tb.SelectAll));
    }

    private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var tb = (TextBox)sender;
        if (!tb.IsKeyboardFocusWithin)
        {
            e.Handled = true;   // avoid placing caret before focus
            tb.Focus();         // GotKeyboardFocus handler will SelectAll
        }
    }

    // --- ComboBox (editable) ---
    private void ComboBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var cb = (ComboBox)sender;
        if (!cb.IsEditable) return;

        cb.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            var inner = cb.Template?.FindName("PART_EditableTextBox", cb) as TextBox;
            inner?.Focus();
            inner?.SelectAll();
        }));
    }

    private void ComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cb = (ComboBox)sender;
        if (!cb.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            cb.Focus(); // then GotKeyboardFocus -> selects all if editable
        }
    }

    static string GetCompany()
    {
        var asm = Application.ResourceAssembly ?? Assembly.GetExecutingAssembly();
        return asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "MyCompany";
    }
    static string GetProduct()
    {
        var asm = Application.ResourceAssembly ?? Assembly.GetExecutingAssembly();
        return asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "MyApp";
    }

    static string EnsureLocalDbCopy()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDir = Path.Combine(local, GetCompany(), GetProduct(), "Data");
        Directory.CreateDirectory(dataDir);

        AppDomain.CurrentDomain.SetData("DataDirectory", dataDir);

        var shipDir = AppContext.BaseDirectory;
        var shipMdf = Path.Combine(shipDir, "Data", "DataBase.mdf");

        var targetMdf = Path.Combine(dataDir, "DataBase.mdf");

        if (File.Exists(shipMdf) && !File.Exists(targetMdf))
            File.Copy(shipMdf, targetMdf);

        // make sure it’s writable
        if (File.Exists(targetMdf))
        {
            var a = File.GetAttributes(targetMdf);
            if ((a & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(targetMdf, a & ~FileAttributes.ReadOnly);
        }

        return dataDir;
    }


    public App()
    {
        builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // presentation services

                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<RetailViewModel>();
                services.AddTransient<RetailView>();
                services.AddTransient<ProductViewModel>();
                services.AddTransient<ProductView>();
                services.AddTransient<WholesaleViewModel>();
           
                services.AddTransient<SaleListViewModel>();
                services.AddTransient<CustomerView>();
				services.AddTransient<CustomerViewModel>();

				services.AddTransient<WelcomeViewModel>();
				services.AddTransient<Welcome>();
				services.AddTransient<LoginViewModel>();
				services.AddTransient<LoginView>();
				services.AddTransient<OrderInvoiceViewModel>();
				services.AddTransient<OrderInvoiceView>();
				services.AddTransient<SummaryWindow>();
				services.AddTransient<SummaryViewModel>();
				services.AddTransient<PaymentReceiptViewModel>();
				services.AddTransient<PaymentReceiptView>();
				services.AddTransient<ReturnView>();
				services.AddTransient<ReturnViewModel>();
				services.AddTransient<CoefficientView>();
				services.AddTransient<CoefficientViewModel>();
				services.AddTransient<AdminViewModel>();
				services.AddTransient<AdminView>();
                services.AddTransient<ReturnInvoiceView>();
				services.AddTransient<BrandViewModel>();
                services.AddTransient<BonusesViewModel>();
                services.AddTransient<BonusesView>();
                services.AddTransient<RoleManagementViewModel>();
                services.AddTransient<PermissionsViewModel>();
                services.AddTransient<PermissionsView>();
               
                services.AddTransient<ReferenceViewModel>();

                services.AddTransient<ReturnInvoiceViewModel>();

                services.AddTransient<CourierInfoViewModel>();

                services.AddTransient<StockViewModel>();

               
                services.AddTransient<TransferViewModel>();
                
                services.AddTransient<ImportViewModel>();

               services.AddTransient<MovementViewModel>();

                services.AddTransient<ReportViewModel>();

                services.AddTransient<StockLocationViewModel>();
                services.AddTransient<PendingOrdersViewModel>();
                services.AddTransient<DeliveryOrdersViewModel>();
                services.AddTransient<AssignPickersViewModel>();
                services.AddTransient<TransferReceiveViewModel>();
                services.AddTransient<ReturnDebtRepaymentViewModel>();
                services.AddTransient<UserViewModel>();
                services.AddTransient<ManagerViewModel>();
				services.AddTransient<BrandViewModel>();
				services.AddTransient<BonusesViewModel>();
				services.AddTransient<ManagerInfoViewModel>();
				services.AddTransient<AssignPickerInfoViewModel>();
                services.AddTransient<CreateAdminViewModel>();
                services.AddTransient<DataBaseViewModel>();
                services.AddTransient<OrganizationInfoView>();
                services.AddTransient<OrganizationInfoViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<ReportsView>();

                services.AddTransient<SalesSummaryView>();
                services.AddTransient<SalesSummaryViewModel>();

                services.AddTransient<RoleManagementView>();
                services.AddTransient<ManagerInfoView>();

                services.AddTransient<CourierInfoView>();
                services.AddTransient<AssignPickerInfoView>();
                services.AddTransient<PriceLevelView>();
                services.AddTransient<PriceLevelViewModel>();
                services.AddTransient<SaleTotalByGroupReportViewModel>();
                services.AddTransient<SaleTotalByGroupReportView>();
                services.AddTransient<SalesByGroupCustomerReportViewModel>();
                services.AddTransient<SalesByGroupCustomerReportView>();
                services.AddTransient<SalesManagerReportViewModel>();
                services.AddTransient<SalesManagerReportView>();
                services.AddTransient<StatisticsViewModel>();
                services.AddTransient<StatisticsView>();
                services.AddTransient<SalesDynamicsStatisticsView>();
                services.AddTransient<SalesDynamicsStatisticsViewModel>();
                services.AddTransient<InactiveWarehouseProductsReportView>();
                services.AddTransient<InactiveWarehouseProductsReportViewModel>();
                services.AddTransient<CourierStorekeeperReportView>();
                services.AddTransient<CourierStorekeeperReportViewModel>();
                services.AddTransient<WarehousePlaceReportViewModel>();
                services.AddTransient<WarehousePlaceReportView>();
                services.AddTransient<CustomerSalesPaymentsReportView>();
                services.AddTransient<CustomerSalesPaymentsReportViewModel>();
                services.AddTransient<TotalSalesReportViewModel>();
                services.AddTransient<TotalSalesReportView>();
                services.AddTransient<OfficialSalesSummaryReportViewModel>();
                services.AddTransient<OfficialSalesSummaryReportView>();
                services.AddTransient<ExpenseCrudView>();
                services.AddTransient<ExpenseCrudViewModel>();
                services.AddTransient<ReturnsDayReportViewModel>();
                services.AddTransient<ReturnsDayReportView>();

                services.AddSingleton<UserSessionService>();
				services.AddSingleton<DataTransferService>();

                services.AddScoped<ProductService>();
				services.AddScoped<BrandService>();
                services.AddScoped<BonusService>();
                services.AddScoped<GroupService>();
				
				services.AddScoped<CurrencyService>();
				services.AddScoped<OrderService>();
				services.AddScoped<CustomerService>();
				services.AddScoped<DialogService>();
				services.AddScoped<ReturnService>();
				services.AddTransient<CoefficientService>();
				services.AddScoped<CustomerFinanceService>();
				services.AddScoped<ReturnReasonService>();
                services.AddScoped<OrganizationInfoService>();
                services.AddScoped<StoreService>();
                services.AddScoped<StoreExchangeService>();
                services.AddScoped<UserService>();
				services.AddScoped<ManagerService>();
				services.AddScoped<StorekeeperService>();
                services.AddScoped<CourierService>();
                services.AddTransient<PdfService>();
                services.AddTransient<CleanService>();
                services.AddTransient<NavigationService>();
                services.AddSingleton<PermissionService>();
                services.AddSingleton<SalesTotalService>();
                services.AddSingleton<ReportService>();
                services.AddSingleton<ExpenseService>();
                services.AddTransient<ReturnsDayReportService>();

                services.AddScoped<ExportHelper>();

                services.AddScoped<ProductRepository>();
				services.AddScoped<BrandRepository>();
				services.AddScoped<CurrencyRepository>();
				services.AddScoped<GroupRepository>();
				
				services.AddScoped<OrderRepository>();
				services.AddScoped<OrderDetailRepository>();
				services.AddScoped<CustomerRepository>();
				services.AddScoped<PriceLevelRepository>();
				services.AddScoped<SalesManagerRepository>();
				services.AddScoped<ReturnRepository>();
				services.AddScoped<ReturnDetailRepository>();
				services.AddScoped<CourierRepository>();
				services.AddScoped<StorekeeperRepository>();
                services.AddScoped<OrganizationInfoRepository>();
                services.AddScoped<StockUpdateLogRepository>();
                services.AddScoped<StoreRepository>();
				services.AddScoped<CategoryRepository>();
                services.AddScoped<RoleRepository>();
                services.AddScoped<PermissionRepository>();
                services.AddScoped<RolePermissionRepository>();

                var dataDir = EnsureLocalDbCopy();

                string connectionString =
                    $@"Data Source=(LocalDB)\MSSQLLocalDB;
                   AttachDbFilename={Path.Combine(dataDir, "DataBase.mdf")};
                   Integrated Security=True;
                   MultipleActiveResultSets=True;
                   Connect Timeout=30";




                services.AddDbContextFactory<DatabaseContext>(options =>
		  options.UseSqlServer(connectionString));


			}).Build();
	}


    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

            EnsureLocalDbCopy();

            builder!.Start();

            // ✅ APPLY MIGRATIONS TO LOCAL USER DB
            using (var scope = builder.Services.CreateScope())
            {
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
                using var db = dbFactory.CreateDbContext();

                db.Database.Migrate();
            }

            var mainWindow = builder!.Services.GetRequiredService<MainWindow>();
            Current.MainWindow = mainWindow;

            mainWindow.Loaded += async (_, __) =>
            {
                try
                {
                    if (mainWindow.DataContext is MainViewModel vm)
                        await vm.InitializeAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Startup Error");
                }
            };

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Critical Startup Error");
        }
    }

}
