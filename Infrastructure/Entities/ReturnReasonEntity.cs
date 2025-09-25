
using System.ComponentModel.DataAnnotations;


namespace Infrastructure.Entities;

public class ReturnReasonEntity
{
	[Key]
	public int Id { get; set; }

	[Required]
	[MaxLength(200)]
	public string Name { get; set; } = null!;

	public bool IsActive { get; set; } = true;

	public virtual ICollection<ReturnEntity> Returns { get; set; } = [];
}