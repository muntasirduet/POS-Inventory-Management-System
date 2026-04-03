using System.ComponentModel.DataAnnotations;

namespace POS.Core.Entities;

public class Permission
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserPermissionOverride> UserOverrides { get; set; } = new List<UserPermissionOverride>();
}
