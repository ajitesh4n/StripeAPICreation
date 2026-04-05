namespace StripUserIntegration.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public string OrderId { get; set; }              // From metadata
        public string StripePaymentIntentId { get; set; }

        public long Amount { get; set; }
        public string Currency { get; set; }

        public string Status { get; set; }               // Pending / Success / Failed

        public string CustomerEmail { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
