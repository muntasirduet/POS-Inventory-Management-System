using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Identity;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class SaleController : Controller
{
    private readonly SaleService _saleService;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly LoyaltyService _loyaltyService;
    private readonly ProductService _productService;

    public SaleController(SaleService saleService, AppDbContext db, UserManager<ApplicationUser> userManager, LoyaltyService loyaltyService, ProductService productService)
    {
        _saleService = saleService; _db = db; _userManager = userManager; _loyaltyService = loyaltyService; _productService = productService;
    }

    [PermissionAuthorize("sales.create")]
    public async Task<IActionResult> Create()
    {
        var branchId = HttpContext.Session.GetInt32("BranchId") ?? 0;
        
        // ← NEW: Auto-set branch for Cashiers on first login
        if (branchId == 0 && User.IsInRole("Cashier"))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId.HasValue == true)
            {
                branchId = user.BranchId.Value;
                HttpContext.Session.SetInt32("BranchId", branchId);
            }
        }
        
        ViewBag.BranchId = branchId;
        return View();
    }

    /// <summary>Get all products for POS terminal - filtered by branch context</summary>
    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> GetProductsForBranch()
    {
        var branchId = HttpContext.Session.GetInt32("BranchId") ?? 0;
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.SKU,
                p.Barcode,
                p.Price,
                p.CostPrice,
                p.CategoryId,
                p.ImageUrl,
                p.Description,
                Category = p.Category!.Name,
                HasVariants = p.Variants.Any(),
                VariantCount = p.Variants.Count()
            })
            .ToListAsync();

        return Json(products);
    }

    /// <summary>Get product attributes for attribute-based selection</summary>
    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> GetProductAttributes(int productId)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.ImageUrl,
                Attributes = p.AttributeValues
                    .GroupBy(av => av.AttributeDefinition.Name)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Values = g.Select(av => av.Value).Distinct().ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (product == null)
            return Json(new { success = false, message = "Product not found" });

        return Json(new { success = true, product });
    }

    [HttpPost, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> Complete([FromBody] CompleteSaleRequest request)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            var userBranchId = HttpContext.Session.GetInt32("BranchId") ?? 0;
            
            // Validate branch context
            if (userBranchId <= 0)
                throw new Exception("Branch context not set. Please select a branch.");
            
            // For non-superadmins, enforce branch restriction
            if (!User.IsInRole("SuperAdmin") && request.BranchId != userBranchId)
                throw new Exception("You can only sell from your assigned branch.");

            var sale = await _saleService.CreateSaleAsync(
                request.Items,
                request.Payments,
                user!.Id,
                request.CustomerId,
                userBranchId,
                request.Discount,
                request.CouponCode
            );
            return Json(new { success = true, saleId = sale.Id });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    [HttpPost, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> Hold([FromBody] HoldSaleRequest request)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            var userBranchId = HttpContext.Session.GetInt32("BranchId") ?? 0;
            var terminalId = HttpContext.Session.GetInt32("TerminalId");

            if (userBranchId <= 0)
                throw new Exception("Branch context not set.");

            if (!User.IsInRole("SuperAdmin") && request.BranchId != userBranchId)
                throw new Exception("You can only hold sales from your assigned branch.");

            var sale = await _saleService.HoldSaleAsync(
                request.Items,
                user!.Id,
                request.CustomerId,
                userBranchId,
                terminalId,
                request.HoldLabel,
                request.HoldNotes,
                request.CouponCode,
                request.ManualDiscount
            );
            return Json(new { success = true, saleId = sale.Id });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    [HttpPost, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> VoidHeld(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var ok = await _saleService.VoidHeldSaleAsync(id, user!.Id);
        return Json(new { success = ok });
    }

    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> HeldSales()
    {
        var user = await _userManager.GetUserAsync(User);
        var sales = await _saleService.GetOnHoldSalesAsync(user!.Id);
        return Json(sales);
    }

    [PermissionAuthorize("sales.view")]
    public async Task<IActionResult> Receipt(int id)
    {
        var sale = await _saleService.GetSaleAsync(id);
        if (sale == null) return NotFound();
        var store = await _db.Stores.FirstOrDefaultAsync();
        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.SaleId == id);
        ViewBag.StoreName = store?.Name ?? "POS Store";
        ViewBag.StorePhone = store?.Phone ?? "";
        ViewBag.ReceiptNumber = receipt?.ReceiptNumber ?? "";
        return View(sale);
    }

    [PermissionAuthorize("sales.view")]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? cashierId)
    {
        var branchId = HttpContext.Session.GetInt32("BranchId");
        var sales = await _saleService.GetSalesAsync(from, to, branchId, cashierId);
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(sales);
    }

    [PermissionAuthorize("sales.view")]
    public async Task<IActionResult> Details(int id)
    {
        var sale = await _saleService.GetSaleAsync(id);
        if (sale == null) return NotFound();
        return View(sale);
    }

    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> ValidateCoupon(string code)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code && !c.IsUsed && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow));
        if (coupon == null) return Json(new { valid = false });
        return Json(new { valid = true, type = coupon.Type.ToString(), value = coupon.Value });
    }

    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> LoyaltyRewards(int customerId)
    {
        var account = await _db.LoyaltyAccounts.FirstOrDefaultAsync(la => la.CustomerId == customerId);
        if (account == null) return Json(new { points = 0, rewards = new object[0] });
        var rewards = await _loyaltyService.GetAvailableRewardsAsync(customerId);
        return Json(new { points = account.Points, tier = account.Tier, rewards = rewards.Select(r => new { r.Id, r.Name, r.PointsRequired, r.DiscountValue }) });
    }

    [HttpPost, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> RedeemReward(int customerId, int rewardId)
    {
        try
        {
            var discount = await _loyaltyService.RedeemRewardAsync(customerId, rewardId);
            return Json(new { success = true, discountValue = discount });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }
    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> ValidateGiftCard(string code)
    {
        var card = await _db.GiftCards.FirstOrDefaultAsync(gc => gc.Code == code && gc.IsActive
            && gc.Balance > 0 && (gc.ExpiresAt == null || gc.ExpiresAt > DateTime.UtcNow));
        if (card == null) return Json(new { valid = false, message = "Invalid or expired gift card." });
        return Json(new { valid = true, balance = card.Balance, code = card.Code });
    }

    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> StoreCreditBalance(int customerId)
    {
        var account = await _db.StoreCreditAccounts.FirstOrDefaultAsync(sc => sc.CustomerId == customerId);
        return Json(new { balance = account?.Balance ?? 0 });
    }

    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> SearchCustomer(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Json(new object[0]);
        var customers = await _db.Customers
            .Where(c => c.Name.Contains(q) || (c.Phone != null && c.Phone.Contains(q)))
            .OrderBy(c => c.Name)
            .Take(10)
            .Select(c => new { c.Id, c.Name, c.Phone, c.Email })
            .ToListAsync();
        return Json(customers);
    }

    [HttpPost, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> RegisterGuest([FromBody] RegisterGuestRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Phone))
            return Json(new { success = false, message = "Phone number is required." });

        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == req.Phone);
        if (existing != null)
            return Json(new { success = true, existing = true, id = existing.Id, name = existing.Name, phone = existing.Phone });

        var customer = new POS.Core.Entities.Customer
        {
            Name = string.IsNullOrWhiteSpace(req.Name) ? $"Guest ({req.Phone})" : req.Name,
            Phone = req.Phone,
            Email = req.Email
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _db.LoyaltyAccounts.Add(new POS.Core.Entities.LoyaltyAccount { CustomerId = customer.Id });
        await _db.SaveChangesAsync();
        return Json(new { success = true, existing = false, id = customer.Id, name = customer.Name, phone = customer.Phone });
    }
}

public class CompleteSaleRequest
{
    public List<CartItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
    public int? CustomerId { get; set; }
    public int? BranchId { get; set; }
    public decimal Discount { get; set; }
    public string? CouponCode { get; set; }
}

public class HoldSaleRequest
{
    public List<CartItemDto> Items { get; set; } = new();
    public int? CustomerId { get; set; }
    public int? BranchId { get; set; }
    public string? HoldLabel { get; set; }
    public string? HoldNotes { get; set; }
    public string? CouponCode { get; set; }
    public decimal ManualDiscount { get; set; } = 0;
}

public class RegisterGuestRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
