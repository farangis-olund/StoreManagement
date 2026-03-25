
namespace Infrastructure.Entities;

public class UserEntity
{
	public int Id { get; set; }
	public string UserName { get; set; } = null!;
	public string FirstName { get; set; } = null!;
	public string LastName { get; set; } = null!;
	public string? Email { get; set; }
	public string Password { get; set; } = null!;
	public string? PhoneNumber { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = [];
}
