namespace Infrastructure.Dtos;

public sealed class OrderRowDto
{
	public DateTime Date { get; set; }
	public string OrderId { get; set; } = "";
	public string CustomerName { get; set; } = "";
	public string Article { get; set; } = "";
	public string ProductName { get; set; } = "";
	public string Brand { get; set; } = "";
	public string Model { get; set; } = "";
	public decimal Quantity { get; set; }
	public decimal Price { get; set; }
}