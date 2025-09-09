using Infrastructure.Dtos;

namespace Infrastructure.Entities;

public class CustomerEntity
{
	public string Id { get; set; } = null!;                      // Код клиента
	public string FullName { get; set; } = null!;                // ФИО
	public string? Email { get; set; }                           // Адрес электронной почты
	public string? MobilePhone { get; set; }                     // Мобильный телефон
	public string? Address { get; set; }                         // Адрес
	public string? City { get; set; }                            // Город
	public string? Region { get; set; }                          // Область
	public string? PostalCode { get; set; }                      // Индекс
	public string? PriceLevelId { get; set; }                    // Уровень
	public string? Notes { get; set; }                           // Примечания
	public double? Debt { get; set; }                            // Задолжность
	public double? Restriction { get; set; }                     // Ограничение
	public double? TotalPurchaseAmount { get; set; }             // КоэфОгрСумЗакупа
	public double? MonthlyRepaymentRate { get; set; }            // КоэфЕжедПогашение
	public double? PlannedPurchaseAmount { get; set; }           // КоэфПланирЗакупа
	public DateTime? ContractDate { get; set; }                  // ДатаКонтракта
	public string? Territory { get; set; }                       // Территория
	public bool? ExcludeMonthlyRepayment { get; set; }           // ИсключитьЕжедПогашение

	public PriceLevelEntity? PriceLevel { get; set; }
	public string? SalesManagerId { get; set; } = null!;
	public SalesManagerEntity? SalesManager { get; set; } = null!;

	public ICollection<CustomerPaymentEntity> Payments { get; set; } = [];

	public ICollection<ReturnEntity> Returns { get; set; } = [];

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
			TotalPurchaseAmount = entity.TotalPurchaseAmount,
			MonthlyRepaymentRate = entity.MonthlyRepaymentRate,
			PlannedPurchaseAmount = entity.PlannedPurchaseAmount,
			ContractDate = entity.ContractDate,
			Territory = entity.Territory,
			ExcludeMonthlyRepayment = entity.ExcludeMonthlyRepayment,
			SalesManagerId = entity.SalesManagerId
		};
	}

}

