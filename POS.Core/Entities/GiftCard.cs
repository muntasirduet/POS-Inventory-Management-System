namespace POS.Core.Entities;

public class GiftCard : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public decimal InitialValue { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public int? IssuedToCustomerId { get; set; }
    public Customer? IssuedToCustomer { get; set; }
    /// <summary>SaleId where this gift card was purchased/issued.</summary>
    public int? IssuedBySaleId { get; set; }
}
