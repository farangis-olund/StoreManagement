namespace Infrastructure.Entities;

public class ReturnDetailEntity
{
	public int Id { get; set; }      // FK
	public string ReturnId { get; set; } = null!; // FK на ReturnEntity
	public string ArticleNumber { get; set; } = null!; // Артикул
	public int Quantity { get; set; }                  // Количество
	public decimal Price { get; set; }                 // Цена за единицу
	public decimal Total { get; set; }                 // Сумма (Quantity * Price)
		
	public virtual ReturnEntity Return { get; set; } = null!;
	public virtual ProductEntity Product { get; set; } = null!;
}
