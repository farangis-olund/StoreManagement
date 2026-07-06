
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
	public int? UnpaidPaymentLimit { get; set; }

	// === Дневные коэффициенты ===
	public double? DailyPurchaseCoefficient { get; set; }        // КоэфДневЗакупа
	public double? DailyRepaymentCoefficient { get; set; }       // КоэфЕжеднПогашение
	public double? DailyPlannedPurchaseCoefficient { get; set; } // КоэфДневЗапланЗакупа

	public DateTime? ContractDate { get; set; }
	public string? Territory { get; set; }
	public bool? ExcludeDailyRepayment { get; set; }             // ИсключитьЕжеднПогашение

	public string? SalesManagerId { get; set; }

    public bool OfficialCustomer { get; set; } = false;

    public string DisplayText => $"{Id} - {FullName}";

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
