
namespace Infrastructure.Entities;

public class UserRoleEntity
{
	public int UserId { get; set; } 
	public int RoleId { get; set; } 
	public virtual UserEntity User { get; set; } = null!;
	public virtual RoleEntity Role { get; set; } = null!;

}
