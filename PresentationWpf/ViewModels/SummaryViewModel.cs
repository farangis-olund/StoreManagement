using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Services;
using PresentationWpf.Views;
using System.Collections.ObjectModel;
using System.Windows;


namespace PresentationWpf.ViewModels;

public partial class SummaryViewModel : ObservableObject

{
	private readonly OrderService _orderService;
	private readonly DataTransferService _dataTransferService;
	private readonly IServiceProvider _serviceProvider;
		
	public SummaryViewModel(OrderService orderService,  
		
		DataTransferService dataTransferService, 
		IServiceProvider serviceProvider) 
	{
		_orderService = orderService;
		_dataTransferService = dataTransferService;
		_serviceProvider = serviceProvider;
		 PaymentDate = DateTime.Today;
		
	}

	public decimal TotalSales { get; private set; }
	public decimal TotalPayments { get; private set; }
	public decimal Balance { get; private set; }
	public string CustomerId { get; set; }

	// ── Add-payment inputs ─────────────────────────────────
	[ObservableProperty] private decimal paymentAmount;     // bound TextBox -> decimal
	[ObservableProperty] private DateTime paymentDate;      // bound DatePicker
	[ObservableProperty] private string? paymentOrderId;    // optional tie to invoice


	public ObservableCollection<OrderRow> Orders { get; } = new();
	public ObservableCollection<PaymentRow> Payments { get; } = new();
	public decimal CustomerBalance { get; private set; }

	public class OrderRow { public DateTime Date { get; set; } public string InvoiceNo { get; set; } = ""; public decimal Amount { get; set; } }
	public class PaymentRow { public DateTime Date { get; set; } public int PaymentNo { get; set; } public decimal Amount { get; set; } }

	// Selection for grids
	[ObservableProperty] private OrderRow? selectedOrder;
	[ObservableProperty] private PaymentRow? selectedPayment;

	public async Task LoadAsync()
	{
		
		Orders.Clear(); Payments.Clear();

		CustomerId = _dataTransferService.CustomerId;

		var orders = await _orderService.GetOrdersByCustomerAsync(CustomerId);
		foreach (var o in orders)
			Orders.Add(new OrderRow { Date = o.Date, InvoiceNo = o.Id, Amount = o.TotalAmount });

		var pays = await _orderService.GetPaymentsByCustomerAsync(CustomerId);
		foreach (var p in pays)
			Payments.Add(new PaymentRow { Date = p.Date, PaymentNo = p.Id, Amount = p.Amount });

		TotalSales = Orders.Sum(x => x.Amount);
		TotalPayments = Payments.Sum(x => x.Amount);
		Balance = TotalSales - TotalPayments;

		OnPropertyChanged(nameof(TotalSales));
		OnPropertyChanged(nameof(TotalPayments));
		OnPropertyChanged(nameof(Balance));
	}

	//// View invoice
	[RelayCommand]
	private async Task ViewInvoice()
	{
		if (SelectedOrder == null)
		{
			MessageBox.Show("Выберите накладную.", "Итоги");
			return;
		}

		// 1) Load the full order (non-barter) by its id from the grid
		var orderEntity = await _orderService.GetOrderAsync(SelectedOrder.InvoiceNo);
		if (orderEntity == null)
		{
			MessageBox.Show("Накладная не найдена.", "Итоги");
			return;
		}

		// 2) Map to CustomerOrder and OrderDetail (what invoice VM expects)
		var order = new CustomerOrder
		{
			Id = orderEntity.Id,
			Date = orderEntity.Date,
			Rate = orderEntity.Rate,
			SuminWords = orderEntity.SuminWords,
			CustomerFullName = orderEntity.Customer?.FullName,
			CustomerAddress = orderEntity.Customer?.Address,
			CustomerPhoneNumber = orderEntity.Customer?.MobilePhone,
			CustomerLevel = orderEntity.Customer?.PriceLevelId,
			UserFullName = orderEntity.User?.FirstName + " " + orderEntity.User?.LastName,
			Customer = orderEntity.Customer!,     // keep nav if you use it in invoice
			OrderDetails = []
		};

		if (orderEntity.OrderDetails != null)
		{
			foreach (var p in orderEntity.OrderDetails)
			{
				order.OrderDetails.Add(new OrderDetail
				{
					ArticleNumber = p.ArticleNumber,
					ProductName = p.Product.ProductName,
					BrandName = p.Product.Brand.BrandName,
					Marka = p.Product.Marka,
					Model = p.Product.Model,
					Quentity = p.Quentity,
					Price = p.Price,              // per-unit price used
					Total = p.Quentity * p.Price,
					WarehousePlace = p.Product.WarehousePlace
				});
			}
		}

		
		// navigate to invoice
		var main = _serviceProvider.GetRequiredService<MainViewModel>();
		var invoiceVm = _serviceProvider.GetRequiredService<OrderInvoiceViewModel>();
		_dataTransferService.SelectedOrder = order;
		_dataTransferService.SelectedCustomerIdForReturn = CustomerId;
		invoiceVm.LoadInvoiceData();
		main.CurrentViewModel = invoiceVm;

		// hide the Summary window while invoice is active
		_serviceProvider.GetRequiredService<DialogService>()
						.Hide<SummaryViewModel>();

	}
	[RelayCommand]
	private async Task ViewPaymentAsync()
	{
		if (SelectedPayment == null)
		{
			MessageBox.Show("Выберите платеж.", "Квитанция");
			return;
		}

		// ensure we have the latest totals (in case of recent changes)
		if (Orders.Count == 0 || Payments.Count == 0)
			await LoadAsync();

		// Calculations:
		// Balance = current остаток after all payments
		// DebtBefore = balance BEFORE this payment = Balance + SelectedPayment.Amount
		var amount = SelectedPayment.Amount;
		var balanceAfter = Balance;
		var debtBefore = Balance + amount;

		var amountInWords = NumberToWordsConverter.ConvertToRussianWords(amount);

		// Build the VM
		var vm = _serviceProvider.GetRequiredService<PaymentReceiptViewModel>();
		vm.CompanyName = "АВТО-ЗАПЧАСТИ"; // or pull from settings
		vm.CustomerCode = CustomerId;
		vm.CustomerFullName = _dataTransferService.SelectedOrder?.CustomerFullName ?? "";
		vm.CustomerAddress = _dataTransferService.SelectedOrder?.CustomerAddress ?? "";
		vm.CustomerPhone = _dataTransferService.SelectedOrder?.CustomerPhoneNumber ?? "";

		vm.PaymentDate = SelectedPayment.Date;
		vm.PaymentNumber = SelectedPayment.PaymentNo;
		vm.Paid = amount;
		vm.Debt = debtBefore;
		vm.Balance = balanceAfter;
		vm.AmountInWords = amountInWords;

		// Create the view and wire it up
		var view = new PaymentReceiptView { DataContext = vm };
		vm.BindView(view);

		// Show as a modal A5 window (owner = active window)
		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
		var win = new Window
		{
			Title = "Квитанция об оплате",
			Content = view,
			Owner = owner,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			SizeToContent = SizeToContent.WidthAndHeight,
			ResizeMode = ResizeMode.NoResize
		};

		// Back button just closes the receipt (Summary stays open behind)
		vm.BackAction = () => win.Close();

		win.ShowDialog();
	}

	[RelayCommand]
	private async Task AddPayment()
	{
		if (PaymentAmount <= 0)
		{
			MessageBox.Show("Сумма платежа должна быть больше 0.", "Платеж");
			return;
		}

		// Try get last non-barter order
		var lastOrderId = await _orderService.GetLastOrderIdForCustomerAsync(CustomerId);

		// If no order exists, just set to null (instead of "0")
		if (string.IsNullOrEmpty(lastOrderId))
			lastOrderId = null;
		var totalSumInWords = NumberToWordsConverter.ConvertToRussianWords(PaymentAmount);

		var saved = await _orderService.AddPaymentAsync(
			customerId: CustomerId,
			amount: PaymentAmount,
			amountInWords: totalSumInWords,
			date: DateTime.Today,
			orderId: lastOrderId);

		if (saved == null)
		{
			MessageBox.Show("Не удалось оформить платеж.", "Платеж");
			return;
		}
		MessageBox.Show("Платеж успешно оформлен.", "Платеж");
		// reset input and refresh
		PaymentAmount = 0;
		await LoadAsync();
	}
		
}
