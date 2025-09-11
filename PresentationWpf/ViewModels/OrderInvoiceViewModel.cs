
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using PresentationWpf.Views;                   // OrderInvoiceView
using PresentationWpf.Services;
using Infrastructure.Dtos;              
using Microsoft.Extensions.DependencyInjection;
using System;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Services;

namespace PresentationWpf.ViewModels;

public partial class OrderInvoiceViewModel : ObservableObject
{
	private readonly DataTransferService _dataTransferService;
	private readonly IServiceProvider _serviceProvider;
	private readonly IDbContextFactory<DatabaseContext> _contextFactory;
	private readonly CustomerFinanceService _financeService;
	public OrderInvoiceViewModel(DataTransferService dataTransferService, IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> contextFactory, CustomerFinanceService customerFinanceService)
	{
		_dataTransferService = dataTransferService;
		_serviceProvider = serviceProvider;
		_contextFactory = contextFactory;
		_financeService = customerFinanceService;
		_ = LoadInvoiceData();
	}

	// ===== Invoice Details =====
	[ObservableProperty] private string _invoiceNumber = string.Empty;
	[ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
	[ObservableProperty] private decimal _totalSum;
	[ObservableProperty] private string _totalSumInWords = string.Empty;

	// ===== Seller =====
	[ObservableProperty] private string _sellerName = string.Empty;

	// ===== Customer =====
	[ObservableProperty] private string _customerName = string.Empty;
	[ObservableProperty] private string _customerAddress = string.Empty;
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
	[ObservableProperty] private decimal _balance;             // итоги


	// The rendered visual to print
	public UserControl? OrderInvoiceViewReference { get; private set; }

	[ObservableProperty] public int _rowCount;
	public async Task LoadInvoiceData()
	{
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
			CustomerPhoneNumber = string.Empty;
			CustomerLevel = string.Empty;
			Rate = 0;
			CustomerCity = string.Empty;
			OrderDetails = [];
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

			CustomerPhoneNumber = order.CustomerPhoneNumber
								  ?? _dataTransferService.SelectedOrder?.CustomerPhoneNumber
								  ?? string.Empty;

			CustomerLevel = order.CustomerLevel?.ToString()
								  ?? _dataTransferService.SelectedOrder?.CustomerLevel?.ToString()
								  ?? string.Empty;

			CustomerCity = order.Customer!.City
								  ?? _dataTransferService.SelectedOrder!.Customer!.City
								  ?? string.Empty;

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

			// Prepare a visual bound to this VM for preview/print/export
			var invoiceView = new OrderInvoiceView { DataContext = this };
			OrderInvoiceViewReference = invoiceView;
			
		}
	}

	[RelayCommand]
	private void Print()
	{
		if (OrderInvoiceViewReference is null)
		{
			MessageBox.Show("Печать чека недоступна: визуал не создан.", "Печать",
							MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		// Find the top bar in the view we are going to print
		var topBar = OrderInvoiceViewReference.FindName("TopBar") as FrameworkElement;
		var oldVis = topBar?.Visibility ?? Visibility.Visible;

		try
		{
			// Hide the button/top bar ONLY for printing
			if (topBar != null)
			{
				topBar.Visibility = Visibility.Collapsed;
				OrderInvoiceViewReference.UpdateLayout();
			}

			var dlg = new PrintDialog();
			// (Optional) show dialog:
			// if (dlg.ShowDialog() != true) return;

			// Use the exact same sizing that worked for you before
			double pageW = dlg.PrintableAreaWidth;
			double pageH = dlg.PrintableAreaHeight;

			OrderInvoiceViewReference.Measure(new Size(pageW, pageH));
			OrderInvoiceViewReference.Arrange(new Rect(new Point(0, 0), new Size(pageW, pageH)));
			OrderInvoiceViewReference.UpdateLayout();

			// Print the WHOLE control (button is hidden)
			dlg.PrintVisual(OrderInvoiceViewReference, "Чек заказа");
		}
		finally
		{
			// Restore UI
			if (topBar != null)
			{
				topBar.Visibility = oldVis;
				OrderInvoiceViewReference.UpdateLayout();
			}
		}
	}

	[RelayCommand]
	private async Task BackToOrder()
	{

		var dialog = _serviceProvider.GetRequiredService<DialogService>();
		var main = _serviceProvider.GetRequiredService<MainViewModel>();
		var retail = _serviceProvider.GetRequiredService<RetailViewModel>();

		//var customerId = _dataTransferService.CustomerId;
		//if (!string.IsNullOrWhiteSpace(customerId))
		//	await retail.SelectCustomerByIdAsync(customerId);
				
		main.CurrentViewModel = retail;
				
		dialog.ShowAgain<SummaryViewModel>();
	}

}
