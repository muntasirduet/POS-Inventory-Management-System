namespace POS.Core.Entities;
public class LoyaltyAccount : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int Points { get; set; } = 0;
    public decimal TotalSpend { get; set; } = 0;
    public string Tier { get; set; } = "Standard";
}
