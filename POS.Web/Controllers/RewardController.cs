using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class RewardController : Controller
{
    private readonly AppDbContext _db;
    public RewardController(AppDbContext db) => _db = db;

    [PermissionAuthorize("settings.manage")]
    public async Task<IActionResult> Index()
    {
        var rewards = await _db.Rewards.OrderBy(r => r.PointsRequired).ToListAsync();
        return View(rewards);
    }

    [PermissionAuthorize("settings.manage")]
    public IActionResult Create() => View(new Reward());

    [HttpPost, PermissionAuthorize("settings.manage"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Reward model)
    {
        if (!ModelState.IsValid) return View(model);
        _db.Rewards.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Reward created.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("settings.manage")]
    public async Task<IActionResult> Edit(int id)
    {
        var reward = await _db.Rewards.FindAsync(id);
        if (reward == null) return NotFound();
        return View(reward);
    }

    [HttpPost, PermissionAuthorize("settings.manage"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Reward model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        _db.Rewards.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Reward updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, PermissionAuthorize("settings.manage"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var reward = await _db.Rewards.FindAsync(id);
        if (reward == null) return NotFound();
        reward.IsActive = !reward.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, PermissionAuthorize("settings.manage"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var reward = await _db.Rewards.FindAsync(id);
        if (reward != null) { _db.Rewards.Remove(reward); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Reward deleted.";
        return RedirectToAction(nameof(Index));
    }
}
