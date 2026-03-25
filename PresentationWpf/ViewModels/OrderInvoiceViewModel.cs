
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using PresentationWpf.Views;                   // OrderInvoiceView
using PresentationWpf.Services;
using Infrastructure.Dtos;              

using Infrastructure.Services;
using System.IO;
using QuestPDF.Fluent;

namespace PresentationWpf.ViewModels;

public partial class OrderInvoiceViewModel : ObservableObject
{
	private readonly DataTransferService _dataTransferService;
	private readonly CustomerFinanceService _financeService;
    private readonly OrganizationInfoService _orgService;
    public OrderInvoiceViewModel(DataTransferService dataTransferService, CustomerFinanceService customerFinanceService, OrganizationInfoService organizationInfoService)
	{
		_dataTransferService = dataTransferService;
		_financeService = customerFinanceService;
		_orgService = organizationInfoService;
		_ = LoadInvoiceData();
	}

	// ===== Invoice Details =====
	[ObservableProperty] private string _invoiceNumber = string.Empty;
	[ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
	[ObservableProperty] private decimal _totalSum;
	[ObservableProperty] private string _totalSumInWords = string.Empty;
    [ObservableProperty] private string _shopName = string.Empty;

    // ===== Seller =====
    [ObservableProperty] private string _sellerName = string.Empty;

	// ===== Customer =====
	[ObservableProperty] private string _customerName = string.Empty;
	[ObservableProperty] private string _customerAddress = string.Empty;
	[ObservableProperty] private string _customerRegion = string.Empty;

    [ObservableProperty] private string _customerPhoneNumber = string.Empty;
	[ObservableProperty] private string _customerLevel = string.Empty;

	// Extra fields commonly shown on the invoice header
	[ObservableProperty] private double _rate;                 // Курс валюты
	[ObservableProperty] private string _customerCity = string.Empty;

	// ===== Order Details =====
	[ObservableProperty] private ObservableCollection<OrderDetail> _orderDetails = [];

	// ===== Totals / Financials =====
	[ObservableProperty] private decimal _currentPayment;      // last_platezh
	[ObservableProperty] private decimal _previousPayments;    // pred_platezh
	[ObservableProperty] private decimal _customerDebt;        // zadolzhnost
	[ObservableProperty] private decimal _creditLimit;         // ogranichenie
	[ObservableProperty] private decimal _currentSale;         // saa
	[ObservableProperty] private decimal _totalSales;          // obsh_prodazha
	[ObservableProperty] private decimal _totalReturns;        // vozvrat
	[ObservableProperty] private decimal _oldDebt;             // стар_долг
	[ObservableProperty] private decimal _debtPayment;         // пог_долг
	[ObservableProperty] private decimal _balance;             // итоги

    public string? CustomerTerritory { get; set; }
    public string? CourierId { get; set; }
    public string? StorekeeperId { get; set; }


    // The rendered visual to print
    public UserControl? OrderInvoiceViewReference { get; private set; }

	[ObservableProperty] public int _rowCount;
	public async Task LoadInvoiceData()
	{
        var shopDisplay = await _orgService.GetShopDisplayAsync();

        var order = _dataTransferService.SelectedOrder;
		if (order is null)
		{
			// reset to defaults if nothing selected
			InvoiceNumber = string.Empty;
			InvoiceDate = DateTime.Now;
			TotalSum = 0m;
			TotalSumInWords = string.Empty;
			SellerName = string.Empty;
			CustomerName = string.Empty;
			CustomerAddress = string.Empty;
            CustomerRegion = string.Empty;
            CustomerPhoneNumber = string.Empty;
			CustomerLevel = string.Empty;
			Rate = 0;
			CustomerCity = string.Empty;
			OrderDetails = [];
			ShopName = shopDisplay ?? "";
		}
		else
		{
			// Map header values
			InvoiceNumber = order.Id ?? string.Empty;
			InvoiceDate = order.Date;
			TotalSumInWords = order.SuminWords ?? string.Empty;
			Rate = order.Rate;

			// Seller (prefer navigation property if present)
			SellerName = order.UserFullName
								 ?? order.UserFullName
								 ?? string.Empty;

			// Customer (prefer navigation property; fall back to flat fields if you keep them on SelectedOrder)
			CustomerName = order.CustomerFullName
								 ?? _dataTransferService.SelectedOrder?.CustomerFullName
								 ?? string.Empty;

			CustomerAddress = order.CustomerAddress
								 ?? _dataTransferService.SelectedOrder?.CustomerAddress
								 ?? string.Empty;

            CustomerRegion = order.CustomerRegion
                                 ?? _dataTransferService.SelectedOrder?.CustomerRegion
                                 ?? string.Empty;

            CustomerPhoneNumber = order.CustomerPhoneNumber
								  ?? _dataTransferService.SelectedOrder?.CustomerPhoneNumber
								  ?? string.Empty;

			CustomerLevel = order.CustomerLevel?.ToString()
								  ?? _dataTransferService.SelectedOrder?.CustomerLevel?.ToString()
								  ?? string.Empty;

			CustomerCity = order.CustomerCity
								  ?? _dataTransferService.SelectedOrder?.Customer!.City
								  ?? string.Empty;

            ShopName = shopDisplay ?? "";
            var details = order.OrderDetails ?? new List<OrderDetail>();
			OrderDetails = new ObservableCollection<OrderDetail>(details);

			RowCount = details.Count;

			// TotalSum from details (prefer Total if it exists; otherwise compute)
			TotalSum = details.Count == 0
				? 0m
				: details.Sum(d =>
				{
					// if your OrderDetail has decimal Total, use it
					if (d.GetType().GetProperty("Total") is { } p &&
						p.GetValue(d) is IConvertible cv)
					{
						return Convert.ToDecimal(cv);
					}

					// fallback: Qty * Price (adjust property names to yours)
					// Quentity/Price can be double/decimal; convert carefully:
					var qty = Convert.ToDecimal(d.GetType().GetProperty("Quentity")?.GetValue(d) ?? 0m);
					var price = Convert.ToDecimal(d.GetType().GetProperty("Price")?.GetValue(d)
											   ?? d.GetType().GetProperty("OriginalPrice")?.GetValue(d)
											   ?? 0m);
					return qty * price;
				});

			//totals from DB ---
		
			var customerId = _dataTransferService.CustomerId;
			
			if (string.IsNullOrEmpty(customerId) && string.IsNullOrEmpty(InvoiceNumber)) return;

			var info = await _financeService.GetFinanceInfoAsync(customerId, InvoiceNumber);

			CurrentSale = info.CurrentSale;
			CurrentPayment = info.CurrentPayment;
			PreviousPayments = info.PreviousPayments;
			CustomerDebt = info.CustomerDebt;
			CreditLimit = info.CreditLimit;
			TotalSales = info.TotalSales;
			TotalReturns = info.TotalReturns;
			OldDebt = info.OldDebt;
			Balance = info.Balance;
			if ((info.CurrentPayment - info.CurrentSale) > 0)
				DebtPayment = info.CurrentPayment - info.CurrentSale;
			else
				DebtPayment = 0;

			// Prepare a visual bound to this VM for preview/print/export
			var invoiceView = new OrderInvoiceView { DataContext = this };
			OrderInvoiceViewReference = invoiceView;
			
		}
	}


    // === Print ===
    [RelayCommand]
    private void Print()
    {
        var document = new Documents.InvoiceDocument(this);
        string folder = Path.Combine(Path.GetTempPath(), "Invoices");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"Invoice_{InvoiceNumber}.pdf");
        document.GeneratePdf(file);

        // Show the PDF inside the preview view
        var preview = new DocumentPreviewView(file);
        var window = new Window
        {
            Title = "Invoice Preview",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        window.ShowDialog();
    }


	

}
