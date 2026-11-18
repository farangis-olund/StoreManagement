namespace Infrastructure.Dtos;

public sealed class OrderSummaryDto
{
	public string Id { get; init; } = "";
	public DateTime Date { get; init; }
	public bool IsBarter { get; init; }
	public decimal TotalAmount { get; init; }
}
