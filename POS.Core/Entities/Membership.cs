using POS.Core.Enums;
namespace POS.Core.Entities;
public class Membership : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public MembershipType MembershipType { get; set; }
    public DateTime ExpiresAt { get; set; }
}
