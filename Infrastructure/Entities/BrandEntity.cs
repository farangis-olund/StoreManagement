
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Infrastructure.Entities;

public class BrandEntity
{    
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    [Required]
    public string BrandName { get; set; } = null!;
	public string? CompanyName { get; set; } 

	// Foreign key for Category
	public int? CategoryId { get; set; }

	// Navigation property
	[ForeignKey(nameof(CategoryId))]
	public virtual CategoryEntity? Category { get; set; }

	// Optional convenience property (redundant if using navigation)
	[NotMapped]
	public string? CategoryName => Category?.CategoryName;

	public virtual ICollection<ProductEntity> Products { get; set; } = [];

	public virtual ICollection<ManagerBrandEntity> ManagerBrands { get; set; } = [];

}
