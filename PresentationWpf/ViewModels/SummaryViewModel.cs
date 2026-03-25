using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;


namespace PresentationWpf.ViewModels;

public partial class SummaryViewModel : ObservableObject

{
	private readonly OrderService _orderService;
    private readonly CustomerService _customerService;
    private readonly DataTransferService _dataTransferService;
	private readonly IServiceProvider _serviceProvider;
	private readonly CustomerFinanceService _financeService;
    private readonly UserSessionService _userSessionService;
    public SummaryViewModel(OrderService orderService,  
		
		DataTransferService dataTransferService, 
		IServiceProvider serviceProvider, CustomerFinanceService financeService, CustomerService customerService, UserSessionService userSessionService) 
	{
		_orderService = orderService;
		_dataTransferService = dataTransferService;
		_serviceProvider = serviceProvider;
		_financeService = financeService;
        _customerService = customerService;
        _userSessionService = userSessionService;
		 PaymentDate = DateTime.Today;
		
	}

	public decimal TotalSales { get; set; }
	public decimal TotalPayments { get; set; }
	public decimal Balance { get; private set; }
	public string CustomerId { get; set; } = null!;

	// ── Add-payment inputs ─────────────────────────────────
	[ObservableProperty] private decimal paymentAmount;     // bound TextBox -> decimal
	[ObservableProperty] private DateTime paymentDate;      // bound DatePicker
	[ObservableProperty] private string? paymentOrderId;    // optional tie to invoice
    [ObservableProperty] private double _exchangeRate = 1;

    public ObservableCollection<OrderRow> Orders { get; } = new();
	public ObservableCollection<PaymentRow> Payments { get; } = new();

    public ObservableCollection<UnpaidOrderDto> UnpaidOrders { get; } = new();
    public decimal CustomerBalance { get; private set; }

	public class OrderRow { public DateTime Date { get; set; } public string InvoiceNo { get; set; } = ""; public decimal Amount { get; set; } }
	public class PaymentRow { public DateTime Date { get; set; } public int PaymentNo { get; set; } public decimal Amount { get; set; } public string? OrderId { get; set; } }

	// Selection for grids
	[ObservableProperty] private OrderRow? selectedOrder;
	[ObservableProperty] private PaymentRow? selectedPayment;
   
    
    public async Task LoadAsync()
	{
		Orders.Clear();
		Payments.Clear();

		CustomerId = _dataTransferService.CustomerId;

		// Load orders
		var orders = await _orderService.GetOrdersByCustomerAsync(CustomerId);
		foreach (var o in orders)
			Orders.Add(new OrderRow { Date = o.Date, InvoiceNo = o.Id, Amount = o.TotalAmount });

		// Load payments
		var pays = await _orderService.GetPaymentsByCustomerAsync(CustomerId);
		foreach (var p in pays)
			Payments.Add(new PaymentRow { Date = p.Date, PaymentNo = p.Id, Amount = p.Amount, OrderId = p.OrderId });

        // Load unpaidPayments
        var unpaidOrders = await _orderService.GetUnpaidOrdersAsync(CustomerId);
        foreach (var o in unpaidOrders)
            UnpaidOrders.Add(new UnpaidOrderDto { Date = o.Date, Id = o.Id, Paid = o.Paid, PaymentId = o.PaymentId });



        // ✅ Use FinanceService for totals
        var finance = _serviceProvider.GetRequiredService<CustomerFinanceService>();
		var financeInfo = await finance.GetFinanceInfoAsync(CustomerId, orderId: null); // or pass selected order id

		TotalSales = financeInfo.TotalSales;
		TotalPayments = financeInfo.PreviousPayments + financeInfo.CurrentPayment;
		Balance = financeInfo.Balance;

		OnPropertyChanged(nameof(TotalSales));
		OnPropertyChanged(nameof(TotalPayments));
		OnPropertyChanged(nameof(Balance));
	}


	partial void OnSelectedOrderChanged(OrderRow? value)
	{
		if (value == null) return;

		_ = LoadSuggestedPaymentAsync(value.InvoiceNo);
	}

    [RelayCommand]
    private async Task ViewInvoice()
    {
        if (SelectedOrder == null)
        {
            MessageBox.Show("Выберите накладную.", "Итоги");
            return;
        }

        // 1️⃣ Load order from DB
        var orderEntity = await _orderService.GetOrderAsync(SelectedOrder.InvoiceNo);
        if (orderEntity == null)
        {
            MessageBox.Show("Накладная не найдена.", "Итоги");
            return;
        }

        // 2️⃣ Map order to what Invoice VM expects
        var order = new CustomerOrder
        {
            Id = orderEntity.Id,

            Date = orderEntity.Date,
            Rate = orderEntity.Rate,
            SuminWords = orderEntity.SuminWords,
            CustomerFullName = orderEntity.Customer?.FullName,
			CustomerRegion = orderEntity.Customer?.Region,
            CustomerAddress = orderEntity.Customer?.Address,
            CustomerPhoneNumber = orderEntity.Customer?.MobilePhone,
            CustomerLevel = orderEntity.Customer?.PriceLevelId,
            UserFullName = $"{orderEntity.User?.FirstName} {orderEntity.User?.LastName}",
            Customer = orderEntity.Customer!,
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
                    Price = p.Price,
                    Total = p.Quentity * p.Price,
                    WarehousePlace = p.Product.WarehousePlace
                });
            }
        }

		// 3️⃣ Prepare Invoice ViewModel
		var invoiceVm = _serviceProvider.GetRequiredService<OrderInvoiceViewModel>();

        // --- Маршрут data
        invoiceVm.CustomerTerritory = orderEntity.Customer?.Territory;
        invoiceVm.CourierId = orderEntity.CourierId;
        invoiceVm.StorekeeperId = orderEntity.StorekeeperId;

        _dataTransferService.SelectedOrder = order;
		await invoiceVm.LoadInvoiceData();

		// 4️⃣ Create the PDF with QuestPDF
		var document = new InvoiceDocument(invoiceVm);
		var pdfService = _serviceProvider.GetRequiredService<PdfService>();
		var pdfBytes = pdfService.GeneratePdfBytes(ReportType.Invoice, invoiceVm);

		var preview = new DocumentPreviewView();
		preview.LoadPdf(pdfBytes); // you'll add this helper below

		var window = new Window
		{
			Title = $"Просмотр накладной № {order.Id}",
			Content = preview,
			Width = 900,
			Height = 900,
			WindowStartupLocation = WindowStartupLocation.CenterScreen
		};

		window.ShowDialog();
	}

    [RelayCommand]
    private async Task ViewPaymentAsync()
    {
        if (SelectedPayment == null)
        {
            MessageBox.Show("Выберите платеж.", "Квитанция");
            return;
        }
        if (SelectedPayment.OrderId != null || SelectedPayment.OrderId != "")
        {
            var orderEntity = await _orderService.GetOrderAsync(SelectedPayment.OrderId);
            if (orderEntity != null)
            {
               orderEntity.IsSent = true;
                await _orderService.UpdateOrderAsync(orderEntity);


            }
        }
        

        // Ensure data is current
        if (Orders.Count == 0 || Payments.Count == 0)
            await LoadAsync();

        var customersEntity = await _customerService.GetCustomerByIdAsync(CustomerId);

        // --- Prepare values ---
        var amount = SelectedPayment.Amount;
        var balanceAfter = Balance;
        var debtBefore = Balance + amount;
        var amountInWords = NumberToWordsConverter.ConvertToRussianWords(amount);

        // --- Build ViewModel ---
        var vm = _serviceProvider.GetRequiredService<PaymentReceiptViewModel>();

        // ✅ Dynamically load shop name from OrganizationInfoService
        var orgService = _serviceProvider.GetRequiredService<OrganizationInfoService>();
        vm.CompanyName = await orgService.GetShopDisplayAsync() ?? "Организация";

        vm.CustomerCode = CustomerId;
        vm.CustomerFullName = customersEntity?.FullName ?? "";
        vm.CustomerAddress = customersEntity?.Address ?? "";
        vm.CustomerPhone = customersEntity ?.MobilePhone ?? "";

        vm.PaymentDate = SelectedPayment.Date;
        vm.PaymentNumber = SelectedPayment.PaymentNo;
        vm.OrderNumber = SelectedPayment.OrderId ?? string.Empty;
        vm.Paid = amount;
        vm.Debt = debtBefore;
        vm.Balance = balanceAfter;
        vm.AmountInWords = amountInWords;

        // --- Generate PDF ---
        var pdfService = _serviceProvider.GetRequiredService<PdfService>();
        var pdfPath = pdfService.GeneratePdf(ReportType.Payment, vm);

        // --- Show PDF in preview window ---
        var preview = new DocumentPreviewView(pdfPath);


        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var win = new Window
        {
            Title = $"Квитанция об оплате № {vm.PaymentNumber}",
            Content = preview,
            Width = 900,
            Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner
        };

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
        var rate = _userSessionService.ExchangeRate;
        ExchangeRate = rate > 0 ? rate : 1;
        // Try get last non-barter order
        var todayOrderId = await _orderService.GetTodayOrderIdForCustomerAsync(CustomerId);

		// If no order exists, just set to null (instead of "0")
		if (string.IsNullOrEmpty(todayOrderId))
			todayOrderId = null;
		var totalSumInWords = NumberToWordsConverter.ConvertToRussianWords(PaymentAmount);

		var saved = await _orderService.AddPaymentAsync(
			customerId: CustomerId,
			amount: PaymentAmount,
			amountInWords: totalSumInWords,
			date: DateTime.Today,
			orderId: todayOrderId, 
            rate: ExchangeRate);

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

    private async Task LoadSuggestedPaymentAsync(string orderId)
    {
        if (string.IsNullOrWhiteSpace(CustomerId))
            return;

        var today = DateTime.Today;

        // 1) Load customer
        var customer = await _customerService.GetCustomerByIdAsync(CustomerId);
        if (customer == null)
            return;

        // 2) Load today's total payments (all orders)
        decimal todayPaymentsAll = await _orderService.GetCustomerPaymentsSumAsync(CustomerId, today);

        // 3) Load today's payments for THIS order only
        decimal todayPaymentsThisOrder = await _orderService.GetOrderPaymentsSumAsync(orderId, today);

        // 4) Load current order total
        var order = await _orderService.GetOrderAsync(orderId);
        decimal currentOrderTotal = order?.OrderDetails?
            .Sum(d => (decimal)(d.Price * d.Quentity)) ?? 0m;

        // ------------------------------
        // RULE 1: ORDER ALREADY PAID TODAY → 0
        // ------------------------------

        if (todayPaymentsThisOrder >= currentOrderTotal && currentOrderTotal > 0)
        {
            PaymentAmount = 0m;
            PaymentOrderId = orderId;
            return;
        }

        // ------------------------------
        // RULE 2: CUSTOMER PAID SOMETHING TODAY (OTHER ORDERS)
        // → FULL CURRENT ORDER AMOUNT
        // ------------------------------

        if (todayPaymentsThisOrder == 0m && todayPaymentsAll > 0m)
        {
            PaymentAmount = currentOrderTotal;
            PaymentOrderId = orderId;
            return;
        }

        // ------------------------------
        // RULE 3: CUSTOMER EXCLUDED FROM DAILY REPAYMENT
        // → REMAINING
        // ------------------------------

        if (customer.ExcludeDailyRepayment == true)
        {
            decimal remaining = Math.Max(0m, currentOrderTotal - todayPaymentsThisOrder);
            PaymentAmount = remaining;
            PaymentOrderId = orderId;
            return;
        }

        // ------------------------------
        // RULE 4: First payment of the day → use normal calculation
        // ------------------------------

        decimal suggested = await _financeService.EzhigodPogashenieAsync(CustomerId, orderId);

        if (suggested <= 0m)
            suggested = currentOrderTotal;

        PaymentAmount = suggested;
        PaymentOrderId = orderId;
    }

}
