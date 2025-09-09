
using Infrastructure.Entities;

namespace Infrastructure.Dtos;

public class SalesManager
{
	public string Id { get; set; } = Guid.NewGuid().ToString(); // Код менеджера
	public string FullName { get; set; } = null!;               // ФИО
	public string? Address { get; set; }                        // Адрес
	public string? Contacts { get; set; }                       // Контакты

	// Computed property: Id + FullName
	public string DisplayText => $"{Id} - {FullName}";

	public static implicit operator SalesManager(SalesManagerEntity e) =>
		new()
		{
			Id = e.Id,
			FullName = e.FullName,
			Address = e.Address,
			Contacts = e.Contacts
		};
}
