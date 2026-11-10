namespace POS.Core.Entities;

/// <summary>Records every loyalty points earn or redeem event for a customer.</summary>
public class LoyaltyTransaction
{
    public int Id { get; set; }
    public int LoyaltyAccountId { get; set; }
    public LoyaltyAccount LoyaltyAccount { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Earn | Redeem | Adjustment | Expiry</summary>
    public string Type { get; set; } = "Earn";
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public int? SaleId { get; set; }
    public int? RewardId { get; set; }
    public Reward? Reward { get; set; }
    public string? Notes { get; set; }
}
