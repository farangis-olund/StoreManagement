using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;
using System.Collections.Generic;

namespace PresentationWpf.ViewModels;

// Each row in "Позиции возврата"
public partial class ReturnLine : ObservableValidator
{
	[ObservableProperty]
	private string article = string.Empty;

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string brand = string.Empty;

	[ObservableProperty]
	private string marka = string.Empty;

	[ObservableProperty]
	private string model = string.Empty;

	[ObservableProperty]
	private string place = string.Empty;

	// How many were purchased in the original order
	[ObservableProperty]
	private int purchasedQty;

	// Editable by operator
	private int _returnQty;
	public int ReturnQty
	{
		get => _returnQty;
		set
		{
			if (SetProperty(ref _returnQty, value))
			{
				ValidateReturnQty();
				// If Total depends on qty, notify
				OnPropertyChanged(nameof(Total));
			}
		}
	}

	// Editable price if you allow it (optional)
	private decimal _price;
	public decimal Price
	{
		get => _price;
		set
		{
			if (SetProperty(ref _price, value))
			{
				ValidatePrice();
				OnPropertyChanged(nameof(Total));
			}
		}
	}

	public decimal Total => ReturnQty * Price;

	// ---- Validation ----
	private void ValidateReturnQty()
	{
		var errors = new List<string>();

		if (ReturnQty < 0)
			errors.Add("Количество к возврату не может быть отрицательным.");
		if (ReturnQty > PurchasedQty)
			errors.Add($"Максимум: {PurchasedQty}.");

		//SetErrors(nameof(ReturnQty), errors);
	}

	private void ValidatePrice()
	{
		var errors = new List<string>();
		if (Price < 0)
			errors.Add("Цена не может быть отрицательной.");
		//SetErrors(nameof(Price), errors);
	}
}
