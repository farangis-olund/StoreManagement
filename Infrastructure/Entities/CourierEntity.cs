
using System.ComponentModel.DataAnnotations;


namespace Infrastructure.Entities;

public class CourierEntity
{
	[Key] public string Id { get; set; } = Guid.NewGuid().ToString();
	[Required, StringLength(120)] public string FullName { get; set; } = null!;
	[StringLength(50)] public string? Phone { get; set; }
	[StringLength(120)] public string? Vehicle { get; set; }
	public bool Active { get; set; } = true;

	public ICollection<OrderEntity> Orders { get; set; } = [];
}
