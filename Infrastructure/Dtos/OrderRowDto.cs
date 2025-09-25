namespace Infrastructure.Dtos;

public sealed class OrderRowDto
{
	public DateTime Date { get; set; }
	public string OrderId { get; set; } = "";
	public string CustomerId { get; set; } = "";
	public string CustomerName { get; set; } = "";
	public string Article { get; set; } = "";
	public string ProductName { get; set; } = "";
	public string Brand { get; set; } = "";
	public string Model { get; set; } = "";
    public string Marka { get; set; } = "";
    public int Quantity { get; set; }
    public int? ReturnedQty { get; set; }
    public decimal Price { get; set; }

}