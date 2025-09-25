

using Infrastructure.Entities;


namespace Infrastructure.Dtos;

public class Product 
{
    public string ArticleNumber { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string Model { get; set; } = null!;
	public string Marka { get; set; } = null!;
	public string Alternative { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public string BrandName { get; set; } = null!;
   
    public int Quentity { get; set; }
	public int OrderQuentity { get; set; }
	public decimal Total { get; set; } = 0;
	public string WarehousePlace { get; set; } = null!;
	public int MinRemainingQuantity { get; set; }
	public double? ExchangeRate { get; set; }
    public double CustomerDiscountPercentage { get; set; } = 0;

	public decimal RetailPriceEuro { get; set; }   // Розн Цена (Euro)
    
	public decimal WholesalePriceEuro { get; set; }   // Опт Цена (Euro)
    	
	public decimal ServicePriceEuro { get; set; }   // Серв Цена (Euro)
    	
	public decimal WholesalePrice1Euro { get; set; }   // Опт Цена 1(Euro)

	public decimal NetPrice { get; set; }   // Цена Нетто

	public decimal SmallWholesalePrice { get; set; }   // Мелкооптовая Цена
     public string? Display {  get; set; }
    public int GroupId { get; set; }
    public int BrandId { get; set; }

    public static implicit operator Product(ProductEntity entity)
    {
        return new Product
        {
            ArticleNumber = entity.ArticleNumber,
            ProductName = entity.ProductName,
            Model = entity.Model,
            Marka = entity.Marka,
            Alternative = entity.Alternative,
            GroupName = entity.Group?.GroupName ?? string.Empty,   // safe
            BrandName = entity.Brand?.BrandName ?? string.Empty,
            Quentity = entity.Quentity,
            WarehousePlace = entity.WarehousePlace,
            RetailPriceEuro = entity.RetailPriceEuro,
            WholesalePriceEuro = entity.WholesalePriceEuro,
            ServicePriceEuro = entity.ServicePriceEuro,
            WholesalePrice1Euro = entity.WholesalePrice1Euro,
            NetPrice = entity.NetPrice,
            SmallWholesalePrice = entity.SmallWholesalePrice,
			Display = $"{entity.ArticleNumber} · {entity.Brand?.BrandName} · {entity.ProductName}",
            GroupId = entity.GroupId,
            BrandId = entity.BrandId,
			OrderQuentity = 0,
            Total = 0,
            
        };
    }
}
