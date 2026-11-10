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
        
        // Load branch info for display
        var branches = await _db.Branches.AsNoTracking().ToListAsync();
        var branchDict = branches.ToDictionary(b => b.Id, b => b.Name);
        
        ViewBag.UserRoles = userRoles;
        ViewBag.Branches = branches;
        ViewBag.BranchDict = branchDict;
        return View(users);
    }

    public async Task<IActionResult> CreateUser()
    {
        await PopulateUserViewBag();
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
            var actorId = _userManager.GetUserId(User);
            if (actorId != null)
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

    /// <summary>Get branches for dropdown - SuperAdmin sees all, Cashiers see only their assigned branch</summary>
    [HttpGet]
    [AllowAnonymous] // Override class-level restriction to allow all authenticated users
    public async Task<IActionResult> GetBranchesForSelector()
    {
        // Only accessible to authenticated users
        if (!User.Identity?.IsAuthenticated ?? true)
            return Unauthorized(new { message = "Not authenticated" });
        
        // SuperAdmin/StoreOwner see all branches
        if (User.IsInRole("SuperAdmin") || User.IsInRole("StoreOwner"))
        {
            var allBranches = await _db.Branches
                .AsNoTracking()
                .Select(b => new { id = b.Id, name = b.Name, isReadOnly = false })
                .ToListAsync();
            return Json(allBranches);
        }
        
        // Cashiers/other roles see only their assigned branch
        var user = await _userManager.GetUserAsync(User);
        if (user?.BranchId == null)
            return Json(new object[0]); // No assigned branch
        
        var userBranch = await _db.Branches
            .AsNoTracking()
            .Where(b => b.Id == user.BranchId)
            .Select(b => new { id = b.Id, name = b.Name, isReadOnly = true })
            .FirstOrDefaultAsync();
        
        return Json(userBranch != null ? new[] { userBranch } : new object[0]);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AllowAnonymous] // Override class-level restriction to allow all authenticated users
    public async Task<IActionResult> SetBranch(int branchId)
    {
        // Only accessible to authenticated users
        if (!User.Identity?.IsAuthenticated ?? true)
            return Json(new { success = false, message = "Not authenticated" });
        
        var branch = await _db.Branches.FindAsync(branchId);
        if (branch == null)
            return Json(new { success = false, message = "Branch not found" });

        // Validate user has access to this branch (except SuperAdmin)
        if (!User.IsInRole("SuperAdmin"))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId != branchId)
                return Json(new { success = false, message = "You don't have access to this branch" });
        }

        HttpContext.Session.SetInt32("BranchId", branchId);
        return Json(new { success = true, message = $"Branch changed to {branch.Name}" });
    }
    
    /// <summary>SuperAdmin assigns a branch to a cashier/user</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignBranchToUser([FromBody] AssignBranchRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.UserId))
            return Json(new { success = false, message = "Invalid request" });
        
        // Only SuperAdmin can assign branches
        if (!User.IsInRole("SuperAdmin"))
            return Json(new { success = false, message = "Only SuperAdmin can assign branches" });
        
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return Json(new { success = false, message = "User not found" });
        
        var branch = await _db.Branches.FindAsync(request.BranchId);
        if (branch == null)
            return Json(new { success = false, message = "Branch not found" });
        
        // Update user's branch
        user.BranchId = request.BranchId;
        var result = await _userManager.UpdateAsync(user);
        
        if (result.Succeeded)
        {
            var actorId = _userManager.GetUserId(User);
            if (actorId != null)
                await _auditService.LogAsync(actorId, "AssignBranchToUser", "User", request.UserId,
                    newValues: $"BranchId={request.BranchId}, BranchName={branch.Name}",
                    ipAddress: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());
            
            return Json(new { success = true, message = $"Branch '{branch.Name}' assigned to {user.FullName}" });
        }
        
        return Json(new { success = false, message = "Failed to update user" });
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
            var actorId = _userManager.GetUserId(User);
            if (actorId != null)
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
        var currentIds = (await _permissionService.GetRolePermissionsAsync(roleId)).Select(p => p.Id).ToHashSet();
        var newIds = (grantedPermissionIds ?? new List<int>()).ToHashSet();

        // Bulk update in a single transaction
        await _permissionService.BulkUpdateRolePermissionsAsync(roleId, newIds);

        var actorId = _userManager.GetUserId(User);
        var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        if (actorId != null)
        {
            var granted = allPerms.Where(p => newIds.Contains(p.Id) && !currentIds.Contains(p.Id)).Select(p => p.Name).ToList();
            var revoked = allPerms.Where(p => !newIds.Contains(p.Id) && currentIds.Contains(p.Id)).Select(p => p.Name).ToList();

            if (granted.Count > 0)
                await _auditService.LogAsync(actorId, "GrantPermissions", "RolePermission", null,
                    newValues: $"Role={role.Name}, Granted=[{string.Join(",", granted)}]",
                    ipAddress: ipAddress);

            if (revoked.Count > 0)
                await _auditService.LogAsync(actorId, "RevokePermissions", "RolePermission", null,
                    oldValues: $"Role={role.Name}, Revoked=[{string.Join(",", revoked)}]",
                    ipAddress: ipAddress);
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

        var granted = (grantedPermissionIds ?? new List<int>()).ToHashSet();
        var actorId = _userManager.GetUserId(User);

        // Bulk replace all overrides in a single DB round-trip
        await _permissionService.ReplaceUserPermissionOverridesAsync(userId, granted);

        if (actorId != null)
            await _auditService.LogAsync(actorId, "UpdateUserPermissionOverrides", "UserPermissionOverride", userId,
                newValues: string.Join(",", granted),
                ipAddress: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Permission overrides for '{user.UserName}' updated.";
        return RedirectToAction(nameof(Users));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task PopulateUserViewBag()
    {
        IQueryable<IdentityRole> rolesQuery = _roleManager.Roles;
        // StoreOwners cannot assign SuperAdmin or StoreOwner roles — only lower roles
        if (!User.IsInRole("SuperAdmin"))
            rolesQuery = rolesQuery.Where(r => r.Name != "SuperAdmin" && r.Name != "StoreOwner");
        ViewBag.Roles = new SelectList(await rolesQuery.ToListAsync(), "Name", "Name");
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
    }
}

/// <summary>Request model for assigning branches to users</summary>
public class AssignBranchRequest
{
    public string UserId { get; set; }
    public int BranchId { get; set; }
}
