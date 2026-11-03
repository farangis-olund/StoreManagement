
using Infrastructure.Entities;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Infrastructure.Dtos;

public class Customer : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
	public string Id { get; set; } = null!;
	public string FullName { get; set; } = null!;
	public string? Email { get; set; }
	public string? MobilePhone { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? Region { get; set; }
	public string? PostalCode { get; set; }
	public string? PriceLevelId { get; set; }
	public string? PriceType { get; set; }
	public int? PriceLevelCode { get; set; }
	public string? Notes { get; set; }

    private double? _debt;
    public double? Debt
    {
        get => _debt;
        set => SetField(ref _debt, value);
    }
    private double? _restriction;
    public double? Restriction
    {
        get => _restriction;
        set => SetField(ref _restriction, value);
    }
    private decimal? _buyLimit;
    public decimal? BuyLimit
    {
        get => _buyLimit;
        set => SetField(ref _buyLimit, value);
    }
    private decimal? _unpaidPaymentLimit;
    public decimal? UnpaidPaymentLimit
    {
        get => _unpaidPaymentLimit;
        set => SetField(ref _unpaidPaymentLimit, value);
    }

	// === Дневные коэффициенты ===
    private double? _dailyPurchaseCoefficient;
    public double? DailyPurchaseCoefficient
    {
        get => _dailyPurchaseCoefficient;
        set => SetField(ref _dailyPurchaseCoefficient, value);
    }
    private double? _dailyRepaymentCoefficient;
    public double? DailyRepaymentCoefficient
    {
        get => _dailyRepaymentCoefficient;
        set => SetField(ref _dailyRepaymentCoefficient, value);
    }
    private double? _dailyPlannedPurchaseCoefficient;
    public double? DailyPlannedPurchaseCoefficient
    {
        get => _dailyPlannedPurchaseCoefficient;
        set => SetField(ref _dailyPlannedPurchaseCoefficient, value);
    }

    private DateTime? _contractDate;
    public DateTime? ContractDate
    {
        get => _contractDate;
        set => SetField(ref _contractDate, value);
    }
	public string? Territory { get; set; }
    private bool? _excludeDailyRepayment;
    public bool? ExcludeDailyRepayment
    {
        get => _excludeDailyRepayment;
        set => SetField(ref _excludeDailyRepayment, value);
    }

	public string? SalesManagerId { get; set; }

    public bool OfficialCustomer { get; set; } = false;

    public string DisplayText => $"{Id} - {FullName}";

    [return: NotNullIfNotNull(nameof(entity))]
	public static implicit operator Customer?(CustomerEntity? entity)
	{
		if (entity == null)
			return null;

		return new Customer
		{
			Id = entity.Id,
			FullName = entity.FullName,
			Email = entity.Email,
			MobilePhone = entity.MobilePhone,
			Address = entity.Address,
			City = entity.City,
			Region = entity.Region,
			PostalCode = entity.PostalCode,
			PriceLevelId = entity.PriceLevelId,
			PriceLevelCode = entity.PriceLevel?.Code ?? 0,   // безопасно
			Notes = entity.Notes,
			Debt = entity.Debt,
			Restriction = entity.Restriction,
			BuyLimit = entity.BuyLimit,
			UnpaidPaymentLimit = entity.UnpaidPaymentLimit,
			OfficialCustomer = entity.OfficialCustomer,
			// --- Коэффициенты ---
			DailyPurchaseCoefficient = entity.DailyPurchaseCoefficient,
			DailyRepaymentCoefficient = entity.DailyRepaymentCoefficient,
			DailyPlannedPurchaseCoefficient = entity.DailyPlannedPurchaseCoefficient,

			ContractDate = entity.ContractDate,
			Territory = entity.Territory,
			ExcludeDailyRepayment = entity.ExcludeDailyRepayment,
			SalesManagerId = entity.SalesManagerId,
			PriceType = entity.PriceLevel?.PriceType        // безопасно
		};
	}

}
