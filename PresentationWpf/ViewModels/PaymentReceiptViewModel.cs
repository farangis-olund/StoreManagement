
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using System.Windows.Controls;
using System.Windows;
using Infrastructure.Services;

namespace PresentationWpf.ViewModels;

public partial class PaymentReceiptViewModel : ObservableObject
{
    private readonly OrganizationInfoService _orgService;
    public PaymentReceiptViewModel(OrganizationInfoService orgService)
	{
        _orgService = orgService;
        _ = LoadShopInfoAsync();
    }

	[ObservableProperty] private string companyName = "";

	// customer
	[ObservableProperty] private string customerFullName = "";
	[ObservableProperty] private string customerCode = "";
	[ObservableProperty] private string customerAddress = "";
	[ObservableProperty] private string customerPhone = "";

	// payment
	[ObservableProperty] private DateTime paymentDate = DateTime.Now;
	[ObservableProperty] private int paymentNumber;
	[ObservableProperty] private decimal debt;
	[ObservableProperty] private decimal paid;
	[ObservableProperty] private decimal balance;
	[ObservableProperty] private string amountInWords = "";
    [ObservableProperty] private string orderNumber = "";
	public UserControl? ViewRef { get; private set; }
	public void BindView(UserControl view) => ViewRef = view;

	// Provide these from caller (e.g., to go back to Summary)
	public Action? BackAction { get; set; }

	[RelayCommand]
	private void Back() => BackAction?.Invoke();

	[RelayCommand]
	private void Print()
	{
		if (ViewRef is null) return;

		var top = ViewRef.FindName("TopBar") as FrameworkElement;
		var old = top?.Visibility ?? Visibility.Visible;

		try
		{
			if (top != null) { top.Visibility = Visibility.Collapsed; ViewRef.UpdateLayout(); }

			var dlg = new PrintDialog();
			double w = 559, h = 794; // A5 portrait in WPF units
			ViewRef.Measure(new Size(w, h));
			ViewRef.Arrange(new Rect(new Point(0, 0), new Size(w, h)));
			ViewRef.UpdateLayout();

			dlg.PrintVisual(ViewRef, "Квитанция об оплате");
		}
		finally
		{
			if (top != null) { top.Visibility = old; ViewRef.UpdateLayout(); }
		}
	}

	// fill from your DTOs
	public void LoadFrom(CustomerPaymentDto dto, CustomerEntity customer,
						 decimal debtBefore, decimal balanceAfter, string words)
	{
		CustomerFullName = customer.FullName;
		CustomerCode = customer.Id;
		CustomerAddress = customer.Address ?? "";
		CustomerPhone = customer.MobilePhone ?? "";

		PaymentDate = dto.Date;
		PaymentNumber = dto.Id;
		Paid = dto.Amount;
		Debt = debtBefore;
		Balance = balanceAfter;
		AmountInWords = words;
	}
    public async Task LoadShopInfoAsync()
    {
        CompanyName = await _orgService.GetShopDisplayAsync() ?? "Организация";
    }
}
