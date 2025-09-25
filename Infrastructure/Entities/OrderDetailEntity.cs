namespace Infrastructure.Entities;

public class OrderDetailEntity
{
	public string OrderId { get; set; } = null!;
	public string ArticleNumber { get; set; } = null!;
	public int Quentity { get; set; }

    public int? ReturnedQty { get; set; }
    public decimal Price { get; set; }
	public virtual OrderEntity Order { get; set; } = null!;
	public virtual ProductEntity Product { get; set; } = null!;

}