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
public class SaleReturnController : Controller
{
    private readonly SaleReturnService _returnService;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public SaleReturnController(SaleReturnService returnService, AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _returnService = returnService; _db = db; _userManager = userManager;
    }

    [PermissionAuthorize("sales.return")]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var branchId = HttpContext.Session.GetInt32("BranchId");
        var returns = await _returnService.GetReturnsAsync(branchId, from, to);
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(returns);
    }

    [PermissionAuthorize("sales.return")]
    public async Task<IActionResult> Details(int id)
    {
        var ret = await _returnService.GetReturnAsync(id);
        if (ret == null) return NotFound();
        return View(ret);
    }

    /// <summary>Step 1: enter the original sale number / ID to look up the sale.</summary>
    [PermissionAuthorize("sales.return")]
    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("sales.return")]
    public async Task<IActionResult> LookupSale(string saleNumber)
    {
        var sale = await _db.Sales
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.SaleNumber == saleNumber);
        if (sale == null)
        {
            TempData["Error"] = $"Sale '{saleNumber}' not found.";
            return RedirectToAction(nameof(Create));
        }
        return RedirectToAction(nameof(NewReturn), new { saleId = sale.Id });
    }

    /// <summary>Step 2: select items and quantities to return.</summary>
    [PermissionAuthorize("sales.return")]
    public async Task<IActionResult> NewReturn(int saleId)
    {
        var sale = await _db.Sales
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == saleId);
        if (sale == null) return NotFound();

        // Pre-compute already-returned qty per item
        var returned = await _db.SaleReturnItems
            .Where(sri => sri.SaleItem.SaleId == saleId)
            .GroupBy(sri => sri.SaleItemId)
            .Select(g => new { SaleItemId = g.Key, Qty = g.Sum(x => x.ReturnQty) })
            .ToDictionaryAsync(x => x.SaleItemId, x => x.Qty);
        ViewBag.AlreadyReturned = returned;
        return View(sale);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("sales.return")]
    public async Task<IActionResult> NewReturn(int saleId, List<int> saleItemIds, List<int> returnQtys,
        string reason, string refundMethod, bool restockItems, string? notes)
    {
        var user = await _userManager.GetUserAsync(User);
        var lines = saleItemIds.Zip(returnQtys, (id, qty) => (id, qty)).ToList();
        try
        {
            var ret = await _returnService.CreateReturnAsync(saleId, user!.Id, reason, refundMethod, restockItems, notes, lines);
            TempData["Success"] = $"Return #{ret.Id} created. Refund: ${ret.RefundAmount:F2}";
            return RedirectToAction(nameof(Details), new { id = ret.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(NewReturn), new { saleId });
        }
    }
}
