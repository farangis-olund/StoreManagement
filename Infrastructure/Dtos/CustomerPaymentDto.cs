namespace Infrastructure.Dtos;

public sealed class CustomerPaymentDto
{
	public int Id { get; init; }                  // № платежа
	public DateTime Date { get; init; }           // Дата
	public decimal Amount { get; init; }          // Сумма
	public string? OrderId { get; init; }         // № накладной (optional)
	
}
