using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Infrastructure.Identity;

namespace POS.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PermissionService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
        => await _db.Permissions.OrderBy(p => p.Module).ThenBy(p => p.Name).ToListAsync();

    public async Task<IEnumerable<Permission>> GetRolePermissionsAsync(string roleId)
        => await _db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .OrderBy(p => p.Module).ThenBy(p => p.Name)
            .ToListAsync();

    public async Task AssignPermissionToRoleAsync(string roleId, int permissionId)
    {
        var exists = await _db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        if (!exists)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokePermissionFromRoleAsync(string roleId, int permissionId)
    {
        var record = await _db.RolePermissions.FindAsync(roleId, permissionId);
        if (record != null)
        {
            _db.RolePermissions.Remove(record);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> UserHasPermissionAsync(string userId, string permissionName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive) return false;

        // 1. Check explicit user override
        var perm = await _db.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
        if (perm == null) return false;

        var userOverride = await _db.UserPermissionOverrides
            .FirstOrDefaultAsync(upo => upo.UserId == userId && upo.PermissionId == perm.Id);
        if (userOverride != null) return userOverride.IsGranted;

        // 2. Fall back to role-based check
        var roleNames = await _userManager.GetRolesAsync(user);

        var roleIds = await _db.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        return await _db.RolePermissions
            .AnyAsync(rp => rp.PermissionId == perm.Id && roleIds.Contains(rp.RoleId));
    }

    public async Task<IEnumerable<PermissionDto>> GetUserEffectivePermissionsAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Enumerable.Empty<PermissionDto>();

        var roleNames = await _userManager.GetRolesAsync(user);
        var roleIds = await _db.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        // Permissions granted via roles
        var rolePerms = await _db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission)
            .Distinct()
            .ToListAsync();

        // User overrides
        var overrides = await _db.UserPermissionOverrides
            .Where(upo => upo.UserId == userId)
            .ToListAsync();

        var overridesById = overrides.ToDictionary(o => o.PermissionId);

        // Start with all permissions, mark effective grant
        var all = await _db.Permissions.OrderBy(p => p.Module).ThenBy(p => p.Name).ToListAsync();

        return all.Select(p =>
        {
            bool granted;
            if (overridesById.TryGetValue(p.Id, out var ov))
                granted = ov.IsGranted;
            else
                granted = rolePerms.Any(rp => rp.Id == p.Id);

            return new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                Module = p.Module,
                Description = p.Description,
                IsGranted = granted
            };
        }).ToList();
    }

    public async Task SetUserPermissionOverrideAsync(string userId, int permissionId, bool isGranted)
    {
        var existing = await _db.UserPermissionOverrides.FindAsync(userId, permissionId);
        if (existing != null)
        {
            existing.IsGranted = isGranted;
        }
        else
        {
            _db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                UserId = userId,
                PermissionId = permissionId,
                IsGranted = isGranted
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task RemoveUserPermissionOverrideAsync(string userId, int permissionId)
    {
        var existing = await _db.UserPermissionOverrides.FindAsync(userId, permissionId);
        if (existing != null)
        {
            _db.UserPermissionOverrides.Remove(existing);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Replaces all user permission overrides in a single transaction:
    /// removes all existing overrides then bulk-inserts the granted set.
    /// </summary>
    public async Task ReplaceUserPermissionOverridesAsync(string userId, IEnumerable<int> grantedPermissionIds)
    {
        var grantedSet = grantedPermissionIds.ToHashSet();

        var existing = await _db.UserPermissionOverrides
            .Where(upo => upo.UserId == userId)
            .ToListAsync();

        _db.UserPermissionOverrides.RemoveRange(existing);

        foreach (var permId in grantedSet)
            _db.UserPermissionOverrides.Add(new UserPermissionOverride { UserId = userId, PermissionId = permId, IsGranted = true });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Replaces role-permission assignments in a single transaction.
    /// </summary>
    public async Task BulkUpdateRolePermissionsAsync(string roleId, IEnumerable<int> newPermissionIds)
    {
        var newIds = newPermissionIds.ToHashSet();

        var current = await _db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        var currentIds = current.Select(rp => rp.PermissionId).ToHashSet();

        var toRemove = current.Where(rp => !newIds.Contains(rp.PermissionId)).ToList();
        var toAdd = newIds.Where(id => !currentIds.Contains(id))
            .Select(id => new RolePermission { RoleId = roleId, PermissionId = id })
            .ToList();

        _db.RolePermissions.RemoveRange(toRemove);
        _db.RolePermissions.AddRange(toAdd);
        await _db.SaveChangesAsync();
    }
}
