
namespace Infrastructure.Entities;

public class RoleEntity
{
	public string Id { get; set; } = Guid.NewGuid().ToString();
	public string RoleName { get; set; } = null!;
	public string Description { get; set; } = null!;
	public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = [];
}
