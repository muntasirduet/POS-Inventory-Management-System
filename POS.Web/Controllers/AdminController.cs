using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Infrastructure.Identity;
using POS.Web.Authorization;
using POS.Web.Models;

namespace POS.Web.Controllers;

[Authorize(Roles = "SuperAdmin,StoreOwner")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IPermissionService _permissionService;
    private readonly AuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IPermissionService permissionService,
        AuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _permissionService = permissionService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    // ── Users ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();
        var userRoles = new Dictionary<string, IList<string>>();
        foreach (var user in users) userRoles[user.Id] = await _userManager.GetRolesAsync(user);
        ViewBag.UserRoles = userRoles;
        return View(users);
    }

    public async Task<IActionResult> CreateUser()
    {
        ViewBag.Roles = new SelectList(await _roleManager.Roles.ToListAsync(), "Name", "Name");
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!ModelState.IsValid) { await PopulateUserViewBag(); return View(model); }
        var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FullName = model.FullName, BranchId = model.BranchId, EmailConfirmed = true };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, model.Role);
            var actorId = _userManager.GetUserId(User)!;
            await _auditService.LogAsync(actorId, "CreateUser", "User", user.Id,
                newValues: $"Email={user.Email}, Role={model.Role}",
                ipAddress: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = "User created.";
            return RedirectToAction(nameof(Users));
        }
        foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
        await PopulateUserViewBag();
        return View(model);
    }

    // ── Branches ──────────────────────────────────────────────────────────

    public async Task<IActionResult> Branches() => View(await _db.Branches.Include(b => b.Store).ToListAsync());

    public async Task<IActionResult> CreateBranch()
    {
        ViewBag.Stores = new SelectList(await _db.Stores.ToListAsync(), "Id", "Name");
        return View(new Branch());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBranch(Branch branch)
    {
        ModelState.Remove("Store"); ModelState.Remove("Terminals"); ModelState.Remove("StockItems"); ModelState.Remove("Sales");
        if (!ModelState.IsValid) { ViewBag.Stores = new SelectList(await _db.Stores.ToListAsync(), "Id", "Name"); return View(branch); }
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Branch created.";
        return RedirectToAction(nameof(Branches));
    }

    // ── Settings ──────────────────────────────────────────────────────────

    public async Task<IActionResult> Settings() => View(await _db.SystemConfigs.ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(List<SystemConfig> configs)
    {
        foreach (var config in configs)
        {
            var existing = await _db.SystemConfigs.FindAsync(config.Id);
            if (existing != null) existing.Value = config.Value;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Settings saved.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetBranch(int branchId)
    {
        HttpContext.Session.SetInt32("BranchId", branchId);
        TempData["Success"] = "Branch context switched.";
        return RedirectToAction("Index", "Home");
    }

    // ── Roles ─────────────────────────────────────────────────────────────

    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Roles() => View(await _roleManager.Roles.ToListAsync());

    [Authorize(Roles = "SuperAdmin"), HttpGet]
    public async Task<IActionResult> CreateRole()
    {
        return View();
    }

    [Authorize(Roles = "SuperAdmin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            ModelState.AddModelError("", "Role name is required.");
            return View();
        }

        if (await _roleManager.RoleExistsAsync(roleName))
        {
            ModelState.AddModelError("", "Role already exists.");
            return View();
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
        if (result.Succeeded)
        {
            var actorId = _userManager.GetUserId(User)!;
            await _auditService.LogAsync(actorId, "CreateRole", "Role", null, newValues: $"RoleName={roleName}",
                ipAddress: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = $"Role '{roleName}' created.";
            return RedirectToAction(nameof(Roles));
        }

        foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
        return View();
    }

    // ── Role Permissions ──────────────────────────────────────────────────

    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> RolePermissions(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role == null) return NotFound();

        var allPerms = await _permissionService.GetAllPermissionsAsync();
        var rolePermIds = (await _permissionService.GetRolePermissionsAsync(roleId)).Select(p => p.Id).ToHashSet();

        var permDtos = allPerms.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Module = p.Module,
            Description = p.Description,
            IsGranted = rolePermIds.Contains(p.Id)
        });

        var vm = new RolePermissionsViewModel
        {
            RoleId = roleId,
            RoleName = role.Name!,
            PermissionsByModule = permDtos.GroupBy(p => p.Module).ToList()
        };

        return View(vm);
    }

    [Authorize(Roles = "SuperAdmin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RolePermissions(string roleId, List<int> grantedPermissionIds)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role == null) return NotFound();

        var allPerms = await _permissionService.GetAllPermissionsAsync();
        var currentlyGranted = (await _permissionService.GetRolePermissionsAsync(roleId)).Select(p => p.Id).ToHashSet();
        var newGranted = (grantedPermissionIds ?? new List<int>()).ToHashSet();

        var actorId = _userManager.GetUserId(User)!;

        foreach (var perm in allPerms)
        {
            if (newGranted.Contains(perm.Id) && !currentlyGranted.Contains(perm.Id))
            {
                await _permissionService.AssignPermissionToRoleAsync(roleId, perm.Id);
                await _auditService.LogAsync(actorId, "GrantPermission", "RolePermission", null,
                    newValues: $"Role={role.Name}, Permission={perm.Name}",
                    ipAddress: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());
            }
            else if (!newGranted.Contains(perm.Id) && currentlyGranted.Contains(perm.Id))
            {
                await _permissionService.RevokePermissionFromRoleAsync(roleId, perm.Id);
                await _auditService.LogAsync(actorId, "RevokePermission", "RolePermission", null,
                    oldValues: $"Role={role.Name}, Permission={perm.Name}",
                    ipAddress: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());
            }
        }

        TempData["Success"] = $"Permissions for role '{role.Name}' updated.";
        return RedirectToAction(nameof(Roles));
    }

    // ── User Permission Overrides ─────────────────────────────────────────

    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UserPermissions(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var effectivePerms = await _permissionService.GetUserEffectivePermissionsAsync(userId);

        var vm = new ManageUserPermissionsViewModel
        {
            UserId = userId,
            UserName = user.UserName!,
            FullName = user.FullName,
            PermissionsByModule = effectivePerms.GroupBy(p => p.Module).ToList()
        };

        return View(vm);
    }

    [Authorize(Roles = "SuperAdmin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UserPermissions(string userId, List<int> grantedPermissionIds)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var allPerms = await _permissionService.GetAllPermissionsAsync();
        var granted = (grantedPermissionIds ?? new List<int>()).ToHashSet();
        var actorId = _userManager.GetUserId(User)!;

        // Remove all existing overrides and reapply only those that differ from the role-inherited value
        foreach (var perm in allPerms)
        {
            await _permissionService.RemoveUserPermissionOverrideAsync(userId, perm.Id);
        }

        foreach (var perm in allPerms)
        {
            if (granted.Contains(perm.Id))
            {
                await _permissionService.SetUserPermissionOverrideAsync(userId, perm.Id, true);
            }
        }

        await _auditService.LogAsync(actorId, "UpdateUserPermissionOverrides", "UserPermissionOverride", userId,
            newValues: string.Join(",", granted),
            ipAddress: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Permission overrides for '{user.UserName}' updated.";
        return RedirectToAction(nameof(Users));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task PopulateUserViewBag()
    {
        ViewBag.Roles = new SelectList(await _roleManager.Roles.ToListAsync(), "Name", "Name");
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
    }
}
