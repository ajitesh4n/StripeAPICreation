namespace StripUserIntegration.Models
{
    public class PaymentRequest
    {
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string PaymentMethodId { get; set; }
        public string ToAccountId { get; set; }
        public long ApplicationFee { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }
}
