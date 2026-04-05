using System.ComponentModel.DataAnnotations;

namespace StripUserIntegration.Models
{
    public class StripeUser
    {
        public class StripeOnboardRequest
        {
            public string Email { get; set; }
            public string Country { get; set; }
            public string BusinessType { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }

        }        
        
    }

}
