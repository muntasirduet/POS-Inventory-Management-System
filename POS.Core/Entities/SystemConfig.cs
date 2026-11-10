namespace POS.Core.Entities;
public class SystemConfig
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Grouping for admin UI display (e.g. Store, Loyalty, Tax, Sales, Inventory).</summary>
    public string? Category { get; set; }
}
