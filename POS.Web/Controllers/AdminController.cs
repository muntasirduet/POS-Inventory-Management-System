using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Infrastructure.Identity;
using POS.Web.Models;

namespace POS.Web.Controllers;

[Authorize(Roles = "SuperAdmin,StoreOwner")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db; _userManager = userManager; _roleManager = roleManager;
    }

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
        if (result.Succeeded) { await _userManager.AddToRoleAsync(user, model.Role); TempData["Success"] = "User created."; return RedirectToAction(nameof(Users)); }
        foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
        await PopulateUserViewBag();
        return View(model);
    }

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

    private async Task PopulateUserViewBag()
    {
        ViewBag.Roles = new SelectList(await _roleManager.Roles.ToListAsync(), "Name", "Name");
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
    }
}
