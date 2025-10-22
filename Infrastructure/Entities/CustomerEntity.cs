using Infrastructure.Dtos;

namespace Infrastructure.Entities;

public class CustomerEntity
{
	public string Id { get; set; } = null!;                 // Код клиента
	public string FullName { get; set; } = null!;           // ФИО
	public string? Email { get; set; }                      // Email
	public string? MobilePhone { get; set; }                // Телефон
	public string? Address { get; set; }                    // Адрес
	public string? City { get; set; }                       // Город
	public string? Region { get; set; }                     // Область
	public string? PostalCode { get; set; }                 // Индекс
	public string? PriceLevelId { get; set; }               // Уровень
	public string? Notes { get; set; }                      // Примечания

	public double? Debt { get; set; }                       // Задолженность
	public double? Restriction { get; set; }                // Ограничение

	// === Три ключевых дневных коэффициента ===
	public double? DailyPurchaseCoefficient { get; set; }        // КоэфДневЗакупа
	public double? DailyRepaymentCoefficient { get; set; }       // КоэфЕжеднПогашение
	public double? DailyPlannedPurchaseCoefficient { get; set; } // КоэфДневЗапланЗакупа

	public DateTime? ContractDate { get; set; }              // Дата контракта
	public string? Territory { get; set; }                   // Территория
	public bool? ExcludeDailyRepayment { get; set; }         // ИсключитьЕжеднПогашение

	public PriceLevelEntity? PriceLevel { get; set; }
	public string? SalesManagerId { get; set; } = null!;
	public SalesManagerEntity? SalesManager { get; set; } = null!;

	public ICollection<CustomerPaymentEntity> Payments { get; set; } = [];
	public ICollection<ReturnEntity> Returns { get; set; } = [];
	public ICollection<OrderEntity> Orders { get; set; } = [];
	public ICollection<ManagerCustomerEntity> ManagerCustomers { get; set; } = [];

	public static implicit operator CustomerEntity(Customer entity)
	{
		return new CustomerEntity
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
			Notes = entity.Notes,
			Debt = entity.Debt,
			Restriction = entity.Restriction,
			DailyPurchaseCoefficient = entity.DailyPurchaseCoefficient,
			DailyRepaymentCoefficient = entity.DailyRepaymentCoefficient,
			DailyPlannedPurchaseCoefficient = entity.DailyPlannedPurchaseCoefficient,
			ContractDate = entity.ContractDate,
			Territory = entity.Territory,
			ExcludeDailyRepayment = entity.ExcludeDailyRepayment,
			SalesManagerId = entity.SalesManagerId
		};
	}
}
