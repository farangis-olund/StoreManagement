using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Entities;

public class ProductEntity
{
    [Key]
    [StringLength(50)]
    [Unicode(false)]
    public string ArticleNumber { get; set; } = null!;
	public int Numbering { get; set; }

	[StringLength(100)]
    public string ProductName { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string Marka { get; set; } = null!;
    public string Alternative { get; set; } = null!;
    public int GroupId { get; set; }
    public int BrandId { get; set; }
	public int Quentity { get; set; }
	public string WarehousePlace { get; set; } = null!;
	public int MinRemainingQuantity { get; set; }
	
	[Column(TypeName = "decimal(18,2)")]
	public decimal PriceLevel1 { get; set; }   // Уровень 1 

    [Column(TypeName = "decimal(18,2)")]
	public decimal PriceLevel2 { get; set; }   // Уровень 2

    [Column(TypeName = "decimal(18,2)")]
	public decimal PriceLevel3 { get; set; }   // Уровень 3

    [Column(TypeName = "decimal(18,2)")]
	public decimal PriceLevel4 { get; set; }   //Уровень 4
       
	[Column(TypeName = "decimal(18,2)")]
	public decimal PriceLevel5 { get; set; }   // Уровень 5

    [ForeignKey("BrandId")]
    public virtual BrandEntity Brand { get; set; } = null!;

    [ForeignKey("GroupId")]
    public virtual GroupEntity Group { get; set; } = null!;
 
    public virtual ICollection<OrderDetailEntity> OrderDetails { get; set; } = [];

}
