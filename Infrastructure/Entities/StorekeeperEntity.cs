
using System.ComponentModel.DataAnnotations;


namespace Infrastructure.Entities;

public class StorekeeperEntity
{
	[Key] public string Id { get; set; } = null!;
	[Required, StringLength(120)] public string FullName { get; set; } = null!;
	[StringLength(50)] public string? Phone { get; set; }
	public bool Active { get; set; } = true;

	public ICollection<OrderEntity> Orders { get; set; } = new List<OrderEntity>();
}
