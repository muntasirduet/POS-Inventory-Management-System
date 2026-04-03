namespace POS.Core.Entities;
public class ReceiptTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HtmlTemplate { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}
