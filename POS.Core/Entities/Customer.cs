namespace POS.Core.Entities;
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public LoyaltyAccount? LoyaltyAccount { get; set; }
    public Membership? Membership { get; set; }
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
