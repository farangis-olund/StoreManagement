
using System.ComponentModel.DataAnnotations;


namespace Infrastructure.Entities;

public class BrandEntity
{    
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    [Required]
    public string BrandName { get; set; } = null!;
	public string CompanyName { get; set; } = null!;

	public virtual ICollection<ProductEntity> Products { get; set; } = new List<ProductEntity>();

}
