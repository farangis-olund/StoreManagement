
using Infrastructure.Entities;

namespace Infrastructure.Dtos;

public class Storekeeper
{
	public string Id { get; set; } = null!;
	public string FullName { get; set; } = null!;
	public string? Phone { get; set; }
	public bool Active { get; set; }
	public string DisplayText => $"{FullName} ({Phone})";

	public static implicit operator Storekeeper(StorekeeperEntity e) =>
		new() { Id = e.Id, FullName = e.FullName, Phone = e.Phone, Active = e.Active };
}