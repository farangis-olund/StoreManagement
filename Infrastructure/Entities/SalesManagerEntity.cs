namespace Infrastructure.Entities;

public class SalesManagerEntity
{
	public string Id { get; set; } = null!; // Код менеджера
	public string FullName { get; set; } = null!;               // ФИО
	public string? Address { get; set; }                        // Адрес
	public string? Contacts { get; set; }                       // Контакты

	public virtual ICollection<CustomerEntity> Customers { get; set; } = [];
	public virtual ICollection<ManagerBrandEntity> ManagerBrands { get; set; } = [];
	public virtual ICollection<ManagerCustomerEntity> ManagerCustomers { get; set; } = [];

}