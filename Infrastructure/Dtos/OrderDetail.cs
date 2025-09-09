
namespace Infrastructure.Dtos;

public class OrderDetail
{
	public string OrderId { get; set; } = null!;
	public string ArticleNumber { get; set; } = null!;
	public int Quentity { get; set; }
	public decimal Price { get; set; }
	public decimal OriginalPrice { get; set; }
	public string ProductName { get; set; } = null!;
	public string Model { get; set; } = null!;
	public string Marka { get; set; } = null!;
	public string BrandName { get; set; } = null!;
	public string WarehousePlace { get; set; } = null!;
	public decimal Total {  get; set; }
	
}
