namespace POS.Core.Entities;

public class StoreCreditAccount : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public decimal Balance { get; set; } = 0;
    public decimal TotalIssued { get; set; } = 0;
    public ICollection<StoreCreditTransaction> Transactions { get; set; } = new List<StoreCreditTransaction>();
}

public class StoreCreditTransaction
{
    public int Id { get; set; }
    public int StoreCreditAccountId { get; set; }
    public StoreCreditAccount Account { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Issue | Redeem | Adjust | Expire</summary>
    public string Type { get; set; } = "Issue";
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public int? SaleId { get; set; }
    public string? Notes { get; set; }
}
