namespace POS.Application.DTOs;

public class PermissionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGranted { get; set; }
}

public class RolePermissionsDto
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public IEnumerable<PermissionDto> Permissions { get; set; } = Enumerable.Empty<PermissionDto>();
}

public class AssignPermissionRequest
{
    public string RoleId { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public bool IsGranted { get; set; }
}
