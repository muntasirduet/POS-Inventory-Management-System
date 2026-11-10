using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class LoyaltyService
{
    private readonly AppDbContext _db;
    public LoyaltyService(AppDbContext db) => _db = db;

    /// <summary>Returns available rewards a customer can redeem given their current points.</summary>
    public async Task<List<Reward>> GetAvailableRewardsAsync(int customerId)
    {
        var account = await _db.LoyaltyAccounts.FirstOrDefaultAsync(la => la.CustomerId == customerId);
        if (account == null) return new();
        return await _db.Rewards.Where(r => r.IsActive && r.PointsRequired <= account.Points).OrderBy(r => r.PointsRequired).ToListAsync();
    }

    /// <summary>Redeems a reward for a customer; returns the DiscountValue to apply to the sale.</summary>
    public async Task<decimal> RedeemRewardAsync(int customerId, int rewardId)
    {
        var account = await _db.LoyaltyAccounts.FirstOrDefaultAsync(la => la.CustomerId == customerId)
            ?? throw new InvalidOperationException("No loyalty account.");
        var reward = await _db.Rewards.FindAsync(rewardId)
            ?? throw new InvalidOperationException("Reward not found.");
        if (!reward.IsActive) throw new InvalidOperationException("Reward is not active.");
        if (account.Points < reward.PointsRequired)
            throw new InvalidOperationException($"Insufficient points. Need {reward.PointsRequired}, have {account.Points}.");

        account.Points -= reward.PointsRequired;
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyAccountId = account.Id, Type = "Redeem",
            Points = -reward.PointsRequired, BalanceAfter = account.Points,
            RewardId = rewardId, Notes = $"Redeemed: {reward.Name}"
        });
        await _db.SaveChangesAsync();
        return reward.DiscountValue;
    }

    /// <summary>Awards points for a completed sale (called by SaleService).</summary>
    public async Task AwardPointsAsync(int customerId, decimal saleTotal, int saleId)
    {
        var account = await _db.LoyaltyAccounts.FirstOrDefaultAsync(la => la.CustomerId == customerId);
        if (account == null) return;

        var cfg = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "LoyaltyPointsPerDollar");
        int rate = int.TryParse(cfg?.Value, out var r) ? r : 1;
        int earned = (int)(saleTotal * rate);
        if (earned <= 0) return;

        account.Points += earned;
        account.TotalSpend += saleTotal;

        // Update tier
        account.Tier = account.TotalSpend switch
        {
            >= 5000 => "Platinum",
            >= 2000 => "Gold",
            >= 500  => "Silver",
            _       => "Standard"
        };

        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyAccountId = account.Id, Type = "Earn",
            Points = earned, BalanceAfter = account.Points,
            SaleId = saleId, Notes = $"Earned from sale ${saleTotal:F2}"
        });
        await _db.SaveChangesAsync();
    }

    public async Task<List<LoyaltyTransaction>> GetTransactionsAsync(int customerId) =>
        await _db.LoyaltyTransactions
            .Include(lt => lt.Reward)
            .Where(lt => lt.LoyaltyAccount.CustomerId == customerId)
            .OrderByDescending(lt => lt.CreatedAt)
            .ToListAsync();
}
