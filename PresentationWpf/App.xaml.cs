
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
using System.Windows;


namespace PresentationWpf;

public partial class App : Application
{
    private static IHost? builder;

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


				services.AddTransient<ReferenceViewModel>();

                services.AddTransient<ReturnInvoiceViewModel>();
                
				
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


				services.AddSingleton<UserSessionService>();
				services.AddSingleton<DataTransferService>();

                services.AddScoped<ProductService>();
				services.AddScoped<BrandService>();
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


				// ObservableColection
				//services.AddTransient<List<Product>>();
				string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=D:\projects\StoreManagementSoftware-main\Infrastructure\Data\DataBase.mdf;Integrated Security=True";
				// datacontext
				// services.AddDbContext<DatabaseContext>(x => x.UseSqlServer(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=D:\projects\StoreManagementSoftware-main\Infrastructure\Data\DataBase.mdf;Integrated Security=True", x => x.MigrationsAssembly(nameof(Infrastructure))));

				services.AddDbContextFactory<DatabaseContext>(options =>
		  options.UseSqlServer(connectionString));


			}).Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        builder!.Start();
        var mainWindow = builder!.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

}
