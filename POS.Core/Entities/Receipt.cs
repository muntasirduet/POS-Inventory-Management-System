namespace POS.Core.Entities;
public class Receipt : BaseEntity
{
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PrintedAt { get; set; }
    public bool EmailSent { get; set; } = false;
}
