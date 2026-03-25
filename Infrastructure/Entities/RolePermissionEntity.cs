
namespace Infrastructure.Entities;

public class RolePermissionEntity
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public RoleEntity Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public PermissionEntity Permission { get; set; } = null!;
}
