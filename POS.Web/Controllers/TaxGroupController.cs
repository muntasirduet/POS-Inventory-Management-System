using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Web.Controllers;

[Authorize(Roles = "SuperAdmin,StoreOwner")]
public class TaxGroupController : Controller
{
    private readonly AppDbContext _db;
    public TaxGroupController(AppDbContext db) => _db = db;

    // ── TaxComponent sub-list ──────────────────────────────────────────────

    public async Task<IActionResult> Components() =>
        View(await _db.TaxComponents.OrderBy(t => t.SortOrder).ToListAsync());

    public IActionResult CreateComponent() => View(new TaxComponent());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateComponent(TaxComponent tc)
    {
        ModelState.Remove("TaxGroups");
        if (!ModelState.IsValid) return View(tc);
        _db.TaxComponents.Add(tc);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tax component created.";
        return RedirectToAction(nameof(Components));
    }

    public async Task<IActionResult> EditComponent(int id)
    {
        var tc = await _db.TaxComponents.FindAsync(id);
        if (tc == null) return NotFound();
        return View(tc);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComponent(int id, TaxComponent tc)
    {
        if (id != tc.Id) return BadRequest();
        ModelState.Remove("TaxGroups");
        if (!ModelState.IsValid) return View(tc);
        _db.TaxComponents.Update(tc);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tax component updated.";
        return RedirectToAction(nameof(Components));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComponent(int id)
    {
        var tc = await _db.TaxComponents.FindAsync(id);
        if (tc != null) { _db.TaxComponents.Remove(tc); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Tax component deleted.";
        return RedirectToAction(nameof(Components));
    }

    // ── TaxGroup CRUD ──────────────────────────────────────────────────────

    public async Task<IActionResult> Index() =>
        View(await _db.TaxGroups
            .Include(g => g.Components).ThenInclude(c => c.TaxComponent)
            .OrderBy(g => g.Name).ToListAsync());

    public async Task<IActionResult> Create()
    {
        ViewBag.AllComponents = await _db.TaxComponents.OrderBy(t => t.SortOrder).ToListAsync();
        return View(new TaxGroup());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaxGroup group, int[]? componentIds)
    {
        ModelState.Remove("Components"); ModelState.Remove("Products");
        if (!ModelState.IsValid) { ViewBag.AllComponents = await _db.TaxComponents.OrderBy(t => t.SortOrder).ToListAsync(); return View(group); }

        // Ensure only one IsDefault
        if (group.IsDefault)
            await _db.TaxGroups.Where(g => g.IsDefault).ForEachAsync(g => g.IsDefault = false);

        _db.TaxGroups.Add(group);
        await _db.SaveChangesAsync();

        if (componentIds != null)
            foreach (var cid in componentIds)
                _db.TaxGroupComponents.Add(new TaxGroupComponent { TaxGroupId = group.Id, TaxComponentId = cid });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Tax group created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var group = await _db.TaxGroups.Include(g => g.Components).FirstOrDefaultAsync(g => g.Id == id);
        if (group == null) return NotFound();
        ViewBag.AllComponents = await _db.TaxComponents.OrderBy(t => t.SortOrder).ToListAsync();
        ViewBag.SelectedComponentIds = group.Components.Select(c => c.TaxComponentId).ToList();
        return View(group);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaxGroup group, int[]? componentIds)
    {
        if (id != group.Id) return BadRequest();
        ModelState.Remove("Components"); ModelState.Remove("Products");
        if (!ModelState.IsValid)
        {
            ViewBag.AllComponents = await _db.TaxComponents.OrderBy(t => t.SortOrder).ToListAsync();
            ViewBag.SelectedComponentIds = componentIds?.ToList() ?? new List<int>();
            return View(group);
        }

        if (group.IsDefault)
            await _db.TaxGroups.Where(g => g.IsDefault && g.Id != id).ForEachAsync(g => g.IsDefault = false);

        _db.TaxGroups.Update(group);

        var existing = await _db.TaxGroupComponents.Where(c => c.TaxGroupId == id).ToListAsync();
        _db.TaxGroupComponents.RemoveRange(existing);
        if (componentIds != null)
            foreach (var cid in componentIds)
                _db.TaxGroupComponents.Add(new TaxGroupComponent { TaxGroupId = id, TaxComponentId = cid });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Tax group updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _db.TaxGroups.FindAsync(id);
        if (group != null) { _db.TaxGroups.Remove(group); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Tax group deleted.";
        return RedirectToAction(nameof(Index));
    }
}
