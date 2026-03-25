
namespace Infrastructure.Entities;

public class PermissionEntity
{
    public int Id { get; set; }

    public string Key { get; set; } = null!;

    public string? Description { get; set; }

    // Navigation
    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = [];
}
