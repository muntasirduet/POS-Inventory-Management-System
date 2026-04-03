using POS.Core.Enums;
namespace POS.Core.Entities;
public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public bool IsSingleUse { get; set; } = true;
    public bool IsUsed { get; set; } = false;
    public DateTime? ExpiresAt { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
