using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Identity;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class ShiftController : Controller
{
    private readonly ShiftService _shiftService;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ShiftController(ShiftService shiftService, AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _shiftService = shiftService; _db = db; _userManager = userManager;
    }

    [PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var branchId = HttpContext.Session.GetInt32("BranchId");
        var shifts = await _shiftService.GetShiftsAsync(branchId, from, to);
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(shifts);
    }

    [PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> Details(int id)
    {
        var shift = await _db.Shifts
            .Include(s => s.Employee)
            .Include(s => s.Branch)
            .Include(s => s.Terminal)
            .Include(s => s.CashEntries)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();
        return View(shift);
    }

    [PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> Open()
    {
        var user = await _userManager.GetUserAsync(User);
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
        if (employee != null)
        {
            var open = await _shiftService.GetOpenShiftAsync(employee.Id);
            if (open != null)
            {
                TempData["Error"] = "You already have an open shift.";
                return RedirectToAction(nameof(Current));
            }
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> Open(decimal openingCash)
    {
        var user = await _userManager.GetUserAsync(User);
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
        if (employee == null)
        {
            TempData["Error"] = "No employee record found. Contact an administrator.";
            return View();
        }
        var branchId = HttpContext.Session.GetInt32("BranchId");
        var terminalId = HttpContext.Session.GetInt32("TerminalId");
        try
        {
            var shift = await _shiftService.OpenShiftAsync(employee.Id, branchId, terminalId, openingCash);
            HttpContext.Session.SetInt32("ShiftId", shift.Id);
            TempData["Success"] = "Shift opened.";
            return RedirectToAction(nameof(Current));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return View();
        }
    }

    [PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> Current()
    {
        var user = await _userManager.GetUserAsync(User);
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
        if (employee == null) return View("NoEmployee");
        var shift = await _shiftService.GetOpenShiftAsync(employee.Id);
        if (shift == null) return RedirectToAction(nameof(Open));
        return View(shift);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> AddCashEntry(int shiftId, string type, decimal amount, string? reason)
    {
        var user = await _userManager.GetUserAsync(User);
        try
        {
            await _shiftService.AddCashEntryAsync(shiftId, type, amount, reason, user!.Id);
            TempData["Success"] = $"{type} of ${amount:F2} recorded.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Current));
    }

    [PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> Close(int id)
    {
        var shift = await _db.Shifts.Include(s => s.CashEntries).Include(s => s.Branch).FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();
        return View(shift);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("shifts.manage")]
    public async Task<IActionResult> Close(int id, decimal closingCash, string? notes,
        [FromForm] List<decimal>? dendenom, [FromForm] List<int>? dencount)
    {
        try
        {
            List<(decimal denomination, int count)>? denominations = null;
            if (dendenom != null && dencount != null && dendenom.Count == dencount.Count)
            {
                denominations = dendenom.Zip(dencount, (d, c) => (d, c))
                    .Where(x => x.c > 0).ToList();
            }
            var shift = await _shiftService.CloseShiftAsync(id, closingCash, notes, denominations);
            HttpContext.Session.Remove("ShiftId");
            TempData["Success"] = "Shift closed.";
            return RedirectToAction(nameof(Details), new { id = shift.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Close), new { id });
        }
    }
}
