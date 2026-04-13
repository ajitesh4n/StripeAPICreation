public class TransferPaymentRequest
{
    public int Id { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; }
    public string DestinationAccountId { get; set; }
    public long? PlatformFee { get; set; }
    public string? ProductName { get; set; }
    public string? PaymentStatus { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? CustomerEmail { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string? StripeSessionId { get; set; } // <-- Add this property to fix CS1061
}