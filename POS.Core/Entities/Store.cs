namespace POS.Core.Entities;
public class Store : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
