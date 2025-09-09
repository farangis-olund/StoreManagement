using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Entities;

public class GroupEntity
{
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    [Required]
    public string GroupName { get; set; } = null!;
    
    public virtual ICollection<ProductEntity> Products { get; set; } = [];
}