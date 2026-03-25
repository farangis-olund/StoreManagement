
namespace Infrastructure.Entities;

public class RoleEntity
{
	public int Id { get; set; } 
	public string RoleName { get; set; } = null!;
	public string Description { get; set; } = null!;
	public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = [];
    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = [];

}
