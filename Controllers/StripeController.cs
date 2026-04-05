using Microsoft.AspNetCore.Mvc;
using Stripe;
using StripUserIntegration.Models;
using static StripUserIntegration.Models.StripeUser;

[ApiController]
[Route("api/[controller]")]
public class StripeController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly StripeUserDbContext _dbContext;
    public StripeController(IConfiguration configuration, StripeUserDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    [HttpPost("onboard")]
    public async Task<IActionResult> OnboardUser([FromBody] StripeOnboardRequest request)
    {
        try { 
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email required.");
        // Use an ISO country code property instead of Name
        var countryCode = request.Country ?? "US";

        var accountOptions = new AccountCreateOptions
        {
            Type = "express",
            Country = countryCode,
            BusinessType = request.BusinessType,
            Individual = new AccountIndividualOptions
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email
            },

            Capabilities = new AccountCapabilitiesOptions
            {
                Transfers = new AccountCapabilitiesTransfersOptions
                {
                    Requested = true
                },
                CardPayments = new AccountCapabilitiesCardPaymentsOptions
                {
                    Requested = true
                }
            }
        };
        var accountService = new AccountService();
        var account = await accountService.CreateAsync(accountOptions);

        // Step 2: Create Account Link (Onboarding URL)
        var linkOptions = new AccountLinkCreateOptions
        {
            Account = account.Id,
            RefreshUrl = "https://connect.stripe.com/reauth",
            ReturnUrl = "https://connect.stripe.com/success",
            Type = "account_onboarding"
        };

        var accountLinkService = new Stripe.AccountLinkService();
        var accountLink = await accountLinkService.CreateAsync(linkOptions);

        var user = new StripeAccounts
        {
            Email = request.Email,
            StripeAccountId = account.Id,
            BusinessType = request.BusinessType,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Country = countryCode,
            OnboardingCompleted = false,
            DetailsSubmitted = false,
            ChargesEnabled = false,
            PayoutsEnabled = false,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.StripeAccounts.Add(user);
        _dbContext.SaveChanges();

        return Ok(new
        {
            message = accountLink.Url,
            AccountId = account.Id,
            OnboardingUrl = accountLink.Url
        });
    }

        catch (Exception ex)
    {
            return StatusCode(500, new
            {
                message = ex.Message,
                details = ex
            });
        }
    }

    [HttpGet("resend-onboarding/{accountId}")]
    public IActionResult ResendOnboarding(string accountId)
    {
        var accountService = new AccountService();
        var account = accountService.Get(accountId);
        var resendOnboardUrl = _configuration["Stripe:ResendOnboardUrl"]+accountId;
        var ReturnUrl = _configuration["Stripe:ReturnUrl"];

        if (account.DetailsSubmitted)
        {
            return Ok("Onboarding already completed");
        }
        var options = new AccountLinkCreateOptions
        {
            Account = accountId,
            RefreshUrl = resendOnboardUrl,
            ReturnUrl = ReturnUrl,
            Type = "account_onboarding",
        };

        var service = new Stripe.AccountLinkService();
        var link = service.Create(options);

        return Ok(new
        {
            AccountId = account.Id,
            OnboardingUrl = link.Url
        });
    }

    [HttpPost("resend-onboarding")]
    public IActionResult ResendOnboardingPost([FromBody] ResendOnboardLink request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.AccountId))
                return BadRequest("AccountId is required");

            var accountService = new AccountService();
            var account = accountService.Get(request.AccountId);

            if (account.DetailsSubmitted)
            {
                return BadRequest("Onboarding already completed");
            }

            var options = new AccountLinkCreateOptions
            {
                Account = request.AccountId,
                RefreshUrl = $"https://connect.stripe.com/api/stripe/resend-onboarding/{request.AccountId}",
                ReturnUrl = "https://connect.stripe.com/success",
                Type = "account_onboarding",
            };

            var service = new AccountLinkService();
            var link = service.Create(options);

            return Ok(new
            {
                onboardingUrl = link.Url
            });
        }
        catch (StripeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                "your_webhook_secret" // from Stripe dashboard
            );

            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    var successIntent = stripeEvent.Data.Object as PaymentIntent;
                    await HandlePaymentSuccess(successIntent);
                    break;

                case "payment_intent.payment_failed":
                    var failedIntent = stripeEvent.Data.Object as PaymentIntent;
                    await HandlePaymentFailed(failedIntent);
                    break;

                case "account.updated":
                    var account = stripeEvent.Data.Object as Account;
                    await UpdateStripeAccount(account);
                    break;

            }

            return Ok();
        }
        catch (StripeException ex)
        {
            return BadRequest($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return BadRequest($"Webhook Error: {ex.Message}");
        }

    }
    private async Task HandlePaymentSuccess(PaymentIntent intent)
    {
        var orderId = intent.Metadata["orderId"];

        var payment = _dbContext.Payments
            .FirstOrDefault(x => x.OrderId == orderId);

        if (payment != null)
        {
            payment.Status = "Success";
            payment.StripePaymentIntentId = intent.Id;
            payment.Amount = intent.Amount;
            payment.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }
    }
    private async Task HandlePaymentFailed(PaymentIntent intent)
    {
        var orderId = intent.Metadata["orderId"];

        var payment = _dbContext.Payments
            .FirstOrDefault(x => x.OrderId == orderId);

        if (payment != null)
        {
            payment.Status = "Failed";
            payment.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }
    }
    private async Task UpdateStripeAccount(Account account)
    {
        var user = _dbContext.StripeAccounts
            .FirstOrDefault(x => x.StripeAccountId == account.Id);

        if (user == null)
            return;

        user.DetailsSubmitted = account.DetailsSubmitted;
        user.ChargesEnabled = account.ChargesEnabled;
        user.PayoutsEnabled = account.PayoutsEnabled;

        user.OnboardingCompleted = account.DetailsSubmitted
                                   && account.ChargesEnabled
                                   && account.PayoutsEnabled;

        user.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    [HttpPost("payment")]
    public IActionResult CreatePayment([FromBody] PaymentRequest request)
    {
        try
        {
            // ✅ STEP 1: Save payment as Pending
            var payment = new Payment
            {
                OrderId = request.Metadata != null && request.Metadata.ContainsKey("orderId")
                            ? request.Metadata["orderId"]
                            : Guid.NewGuid().ToString(),

                Amount = request.Amount,
                Currency = request.Currency,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.Payments.Add(payment);
            _dbContext.SaveChanges();

            // ✅ STEP 2: Create Stripe PaymentIntent
            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = request.PaymentMethodId,
                Confirm = true,

                TransferData = new PaymentIntentTransferDataOptions
                {
                    Destination = request.ToAccountId
                },

                ApplicationFeeAmount = request.ApplicationFee,

                Metadata = request.Metadata ?? new Dictionary<string, string>()
            };

            // 🔥 Ensure orderId is always passed to Stripe
            if (!options.Metadata.ContainsKey("orderId"))
            {
                options.Metadata.Add("orderId", payment.OrderId);
            }

            var service = new PaymentIntentService();
            var paymentIntent = service.Create(options);

            // ✅ STEP 3: Update DB with Stripe PaymentIntentId
            payment.StripePaymentIntentId = paymentIntent.Id;
            payment.UpdatedDate = DateTime.UtcNow;

            _dbContext.SaveChanges();

            return Ok(new
            {
                clientSecret = paymentIntent.ClientSecret,
                paymentId = payment.Id
            });
        }
        catch (StripeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest($"API Error: {ex.Message}");
        }
    }
}