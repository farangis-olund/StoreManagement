using Infrastructure.Dtos;
using System.ComponentModel;

namespace PresentationWpf.Dtos;

public class ProductModel : INotifyPropertyChanged
{
	// ── backing data from DB (Euro) ────────────────────────────────────────────
	private decimal _priceLevel1;
	private decimal _priceLevel2;
	private decimal _priceLevel3;
	private decimal _priceLevel4;
	private decimal _priceLevel5;
	

	// ── basic info ────────────────────────────────────────────────────────────
	public string ArticleNumber { get; set; } = null!;
	public string ProductName { get; set; } = null!;
	public string Model { get; set; } = null!;
	public string Marka { get; set; } = null!;
	public string Alternative { get; set; } = null!;
	public string GroupName { get; set; } = null!;
	public string BrandName { get; set; } = null!;
	public int Quentity { get; set; }
	public string WarehousePlace { get; set; } = null!;
	public int MinRemainingQuantity { get; set; }

    public bool IsInvalid { get; set; }

    private int _orderQuentity;
	private decimal _total;
	private double _exchangeRate = 1;
	private decimal? _barterPriceSom;
	public decimal? CustomerPriceSom =>
	CustomerPrice.HasValue ? Math.Round(CustomerPrice.Value, 2) : (decimal?)null;
	public decimal? BarterPriceSom
	{
		get => _barterPriceSom;
		set
		{
			if (_barterPriceSom != value)
			{
				_barterPriceSom = value;
				OnPropertyChanged(nameof(BarterPriceSom));
				OnPropertyChanged(nameof(EffectivePriceSom));
				UpdateTotal();
			}
		}
	}

	// What the UI uses for display & totals
	public decimal? EffectivePriceSom =>
		BarterPriceSom ?? CustomerPriceSom;

	public void ClearBarterPrice() => BarterPriceSom = null;

	// ── exchange rate ─────────────────────────────────────────────────────────
	public double ExchangeRate
	{
		get => _exchangeRate;
		set
		{
			if (Math.Abs(_exchangeRate - value) > double.Epsilon)
			{
				_exchangeRate = value <= 0 ? 1 : value;
				OnPropertyChanged(nameof(ExchangeRate));
				OnPropertyChanged(nameof(RetailPrice));
				OnPropertyChanged(nameof(CustomerPriceSom));   
				UpdateTotal();
			}
		}
	}

	// ── ONLY TWO PUBLIC PRICES ────────────────────────────────────────────────
	// 1) Retail (converted)
	public decimal RetailPrice => _priceLevel1 * (decimal)ExchangeRate;

	// Level: 1=Retail, 2=Wholesale, 3=Service, 4=Wholesale1, 5=Net, 6=SmallWholesale
	private int? _customerPriceLevel = null;
	public int? CustomerPriceLevel
	{
		get => _customerPriceLevel;
		set
		{
			if (_customerPriceLevel != value)
			{
				_customerPriceLevel = value;
				OnPropertyChanged(nameof(CustomerPriceLevel));
				OnPropertyChanged(nameof(CustomerPrice));
				OnPropertyChanged(nameof(CustomerPriceSom));
				UpdateTotal();
			}
		}
	}

	// 2) Customer price (EUR). Null when no customer selected.
	public decimal? CustomerPrice
	{
		get
		{
			if (_customerPriceLevel is null) return null;
			decimal baseEuro = _customerPriceLevel.Value switch
			{
				1 => _priceLevel1,
				2 => _priceLevel2,
				3 => _priceLevel3,
				4 => _priceLevel4,
				5 => _priceLevel5,
				
				_ => _priceLevel1
			};
			return baseEuro; // no exchange conversion
		}
	}
		

	// Optional discount (%)
	private double _customerDiscountPercentage;
	public double CustomerDiscountPercentage
	{
		get => _customerDiscountPercentage;
		set
		{
			if (Math.Abs(_customerDiscountPercentage - value) > double.Epsilon)
			{
				_customerDiscountPercentage = value;
				UpdateTotal();
				OnPropertyChanged(nameof(CustomerDiscountPercentage));
			}
		}
	}

	// Order qty & total
	public int OrderQuentity
	{
		get => _orderQuentity;
		set
		{
			if (_orderQuentity != value)
			{
				_orderQuentity = value > Quentity ? Quentity : Math.Max(0, value);
				OnPropertyChanged(nameof(OrderQuentity));
				UpdateTotal();
			}
		}
	}

	public decimal Total
	{
		get => _total;
		private set
		{
			if (_total != value)
			{
				_total = value;
				OnPropertyChanged(nameof(Total));
			}
		}
	}

	private void UpdateTotal()
	{
		var unit = EffectivePriceSom ?? 0m;
		var line = OrderQuentity * unit;
		if (CustomerDiscountPercentage > 0)
		{
			var factor = 1m - (decimal)(CustomerDiscountPercentage / 100.0);
			if (factor < 0m) factor = 0m;
			line *= factor;
		}
		Total = Math.Round(line, 2);
	}

	public void RefreshPrices()
	{
		OnPropertyChanged(nameof(RetailPrice));
		OnPropertyChanged(nameof(CustomerPrice));
		OnPropertyChanged(nameof(CustomerPriceSom));
		UpdateTotal();
	}

	// INotifyPropertyChanged
	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged(string propertyName) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	// ── Mapping from DTO ───────────────────────────────────────────────────────
	public static implicit operator ProductModel(Product dto)
	{
		var m = new ProductModel
		{
			ArticleNumber = dto.ArticleNumber,
			ProductName = dto.ProductName,
			Model = dto.Model,
			Marka = dto.Marka,
			Alternative = dto.Alternative,
			GroupName = dto.GroupName,
			BrandName = dto.BrandName,
			Quentity = dto.Quentity,
			WarehousePlace = dto.WarehousePlace,
			MinRemainingQuantity = dto.MinRemainingQuantity,

			_priceLevel1 = dto.PriceLevel1,
			_priceLevel2 = dto.PriceLevel2,
			_priceLevel3 = dto.PriceLevel3,
			_priceLevel4 = dto.PriceLevel4,
			_priceLevel5 = dto.PriceLevel5,

			// If dto.ExchangeRate is decimal? use 1m and convert once:
			ExchangeRate = dto.ExchangeRate is double d ? (d > 0 ? d : 1.0) : 1.0,

			CustomerPriceLevel = null,
			OrderQuentity = 0
		};

		// ✅ Ensure bindings get an initial value
		m.RefreshPrices();
		return m;
	}

}
