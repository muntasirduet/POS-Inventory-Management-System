using POS.Application.DTOs;
using POS.Core.Entities;

namespace POS.Application.Services;

public interface IPermissionService
{
    Task<bool> UserHasPermissionAsync(string userId, string permissionName);
    Task<IEnumerable<Permission>> GetAllPermissionsAsync();
    Task<IEnumerable<Permission>> GetRolePermissionsAsync(string roleId);
    Task AssignPermissionToRoleAsync(string roleId, int permissionId);
    Task RevokePermissionFromRoleAsync(string roleId, int permissionId);
    Task BulkUpdateRolePermissionsAsync(string roleId, IEnumerable<int> newPermissionIds);
    Task<IEnumerable<PermissionDto>> GetUserEffectivePermissionsAsync(string userId);
    Task SetUserPermissionOverrideAsync(string userId, int permissionId, bool isGranted);
    Task RemoveUserPermissionOverrideAsync(string userId, int permissionId);
    Task ReplaceUserPermissionOverridesAsync(string userId, IEnumerable<int> grantedPermissionIds);
}
