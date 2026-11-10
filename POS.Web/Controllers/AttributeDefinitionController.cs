using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Web.Controllers;

[Authorize(Roles = "SuperAdmin,StoreOwner,StoreManager")]
public class AttributeDefinitionController : Controller
{
    private readonly AppDbContext _db;
    public AttributeDefinitionController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.AttributeDefinitions.Include(a => a.Options).OrderBy(a => a.SortOrder).ToListAsync());

    public IActionResult Create() => View(new AttributeDefinition());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AttributeDefinition attr, string[]? optionValues)
    {
        ModelState.Remove("Options"); ModelState.Remove("CategoryTemplates");
        if (!ModelState.IsValid) return View(attr);

        _db.AttributeDefinitions.Add(attr);
        await _db.SaveChangesAsync();

        if (optionValues != null)
        {
            int order = 0;
            foreach (var val in optionValues.Where(v => !string.IsNullOrWhiteSpace(v)))
                _db.AttributeOptions.Add(new AttributeOption { AttributeDefinitionId = attr.Id, Value = val.Trim(), SortOrder = order++ });
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Attribute created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var attr = await _db.AttributeDefinitions.Include(a => a.Options.OrderBy(o => o.SortOrder)).FirstOrDefaultAsync(a => a.Id == id);
        if (attr == null) return NotFound();
        return View(attr);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AttributeDefinition attr, string[]? optionValues, int[]? optionIds, int[]? optionOrders)
    {
        if (id != attr.Id) return BadRequest();
        ModelState.Remove("Options"); ModelState.Remove("CategoryTemplates");
        if (!ModelState.IsValid) return View(attr);

        _db.AttributeDefinitions.Update(attr);

        // Sync options: remove old and re-add
        var existingOptions = await _db.AttributeOptions.Where(o => o.AttributeDefinitionId == id).ToListAsync();
        _db.AttributeOptions.RemoveRange(existingOptions);

        if (optionValues != null)
        {
            int order = 0;
            foreach (var val in optionValues.Where(v => !string.IsNullOrWhiteSpace(v)))
                _db.AttributeOptions.Add(new AttributeOption { AttributeDefinitionId = id, Value = val.Trim(), SortOrder = order++ });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Attribute updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var attr = await _db.AttributeDefinitions.FindAsync(id);
        if (attr != null) { _db.AttributeDefinitions.Remove(attr); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Attribute deleted.";
        return RedirectToAction(nameof(Index));
    }
}
