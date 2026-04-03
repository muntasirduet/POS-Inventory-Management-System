namespace POS.Core.Entities;
public class Currency
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}
