using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class SystemConfigController : Controller
{
    private readonly AppDbContext _db;
    public SystemConfigController(AppDbContext db) => _db = db;

    [PermissionAuthorize("settings.manage")]
    public async Task<IActionResult> Index()
    {
        var configs = await _db.SystemConfigs.OrderBy(c => c.Category).ThenBy(c => c.Key).ToListAsync();
        var grouped = configs.GroupBy(c => c.Category ?? "General")
            .ToDictionary(g => g.Key, g => g.ToList());
        return View(grouped);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("settings.manage")]
    public async Task<IActionResult> Update(int id, string value)
    {
        var config = await _db.SystemConfigs.FindAsync(id);
        if (config == null) return NotFound();
        config.Value = value;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Setting '{config.Key}' updated.";
        return RedirectToAction(nameof(Index));
    }
}
