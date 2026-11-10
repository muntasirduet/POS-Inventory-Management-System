using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class GiftCardService
{
    private readonly AppDbContext _db;
    public GiftCardService(AppDbContext db) => _db = db;

    public async Task<GiftCard> IssueAsync(decimal value, DateTime? expiresAt, int? customerId, string prefix = "GC")
    {
        var code = $"{prefix}-{Guid.NewGuid().ToString("N").ToUpper()[..10]}";
        var card = new GiftCard
        {
            Code = code,
            InitialValue = value,
            Balance = value,
            IsActive = true,
            ExpiresAt = expiresAt,
            IssuedToCustomerId = customerId
        };
        _db.GiftCards.Add(card);
        await _db.SaveChangesAsync();
        return card;
    }

    public async Task<GiftCard?> ValidateAsync(string code)
        => await _db.GiftCards.Include(gc => gc.IssuedToCustomer)
            .FirstOrDefaultAsync(gc => gc.Code == code && gc.IsActive
                && gc.Balance > 0
                && (gc.ExpiresAt == null || gc.ExpiresAt > DateTime.UtcNow));

    /// <summary>Applies up to <paramref name="amountNeeded"/> from the card. Returns actual amount redeemed.</summary>
    public async Task<decimal> RedeemAsync(string code, decimal amountNeeded, int? saleId = null)
    {
        var card = await ValidateAsync(code)
            ?? throw new InvalidOperationException("Gift card is invalid, expired, or has no balance.");
        var redeemed = Math.Min(card.Balance, amountNeeded);
        card.Balance -= redeemed;
        if (card.Balance == 0) card.IsActive = false;
        await _db.SaveChangesAsync();
        return redeemed;
    }

    public async Task<List<GiftCard>> GetAllAsync(int? customerId = null)
    {
        var q = _db.GiftCards.Include(gc => gc.IssuedToCustomer).AsQueryable();
        if (customerId.HasValue) q = q.Where(gc => gc.IssuedToCustomerId == customerId.Value);
        return await q.OrderByDescending(gc => gc.CreatedAt).ToListAsync();
    }
}
