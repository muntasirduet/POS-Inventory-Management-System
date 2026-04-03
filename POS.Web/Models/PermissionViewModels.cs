using POS.Application.DTOs;

namespace POS.Web.Models;

public class RolePermissionsViewModel
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public IEnumerable<IGrouping<string, PermissionDto>> PermissionsByModule { get; set; }
        = Enumerable.Empty<IGrouping<string, PermissionDto>>();
}

public class ManageUserPermissionsViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public IEnumerable<IGrouping<string, PermissionDto>> PermissionsByModule { get; set; }
        = Enumerable.Empty<IGrouping<string, PermissionDto>>();
}
