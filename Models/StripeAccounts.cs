namespace StripUserIntegration.Models
{
    public class StripeAccounts
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string StripeAccountId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string BusinessType { get; set; }
        public string Country { get; set; }
        public bool DetailsSubmitted { get; set; }
        public bool ChargesEnabled { get; set; }
        public bool PayoutsEnabled { get; set; }
        public bool OnboardingCompleted { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

    }
}
