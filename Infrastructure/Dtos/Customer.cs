
using Infrastructure.Entities;

namespace Infrastructure.Dtos;

public class Customer
{
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
	public double? Debt { get; set; }
	public double? Restriction { get; set; }
	public double? TotalPurchaseAmount { get; set; }
	public double? MonthlyRepaymentRate { get; set; }
	public double? PlannedPurchaseAmount { get; set; }
	public DateTime? ContractDate { get; set; }
	public string? Territory { get; set; }
	public bool? ExcludeMonthlyRepayment { get; set; }

	public string? SalesManagerId { get; set; }

	public string DisplayText => $"{Id} - {FullName}";

	public static implicit operator Customer(CustomerEntity entity)
	{
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
			PriceLevelCode= entity.PriceLevel!.Code,
			Notes = entity.Notes,
			Debt = entity.Debt,
			Restriction = entity.Restriction,
			TotalPurchaseAmount = entity.TotalPurchaseAmount,
			MonthlyRepaymentRate = entity.MonthlyRepaymentRate,
			PlannedPurchaseAmount = entity.PlannedPurchaseAmount,
			ContractDate = entity.ContractDate,
			Territory = entity.Territory,
			ExcludeMonthlyRepayment = entity.ExcludeMonthlyRepayment,
			SalesManagerId = entity.SalesManagerId,
			PriceType = entity.PriceLevel.PriceType
		};
	}
}
