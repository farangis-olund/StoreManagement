
namespace Infrastructure.Entities;

public class UserRoleEntity
{
	public string UserId { get; set; } = null!;
	public string RoleId { get; set; } = null!;
	public virtual UserEntity User { get; set; } = null!;
	public virtual RoleEntity Role { get; set; } = null!;
}
