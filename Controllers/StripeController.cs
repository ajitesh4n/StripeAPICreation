using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Stripe;
using Stripe.Checkout;
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
            var existingUser = _dbContext.StripeAccounts
                .FirstOrDefault(x => x.Email == request.Email);
            if (existingUser != null)
            {
                return BadRequest(new
                {
                    message = _configuration["Stripe:DuplicateUser"]
                });
            }
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email required.");
        // Use an ISO country code property instead of Name
        var countryCode = request.Country ?? "US";

        var accountOptions = new AccountCreateOptions
        {
            Type = _configuration["Stripe:OnboardType"],
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
            RefreshUrl = _configuration["Stripe:RefreshUrl"],
            ReturnUrl = _configuration["Stripe:ReturnUrl"],
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
            return BadRequest(new
            {
                message = ex.Message
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
            return Ok(_configuration["Stripe:OnboardAlreadyDone"]);
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
            var resendOnboardUrl = _configuration["Stripe:ResendOnboardUrl"] + request.AccountId;
            var ReturnUrl = _configuration["Stripe:ReturnUrl"];
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
                RefreshUrl = resendOnboardUrl,
                ReturnUrl = ReturnUrl,
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
                 _configuration["Stripe:WebhookSecret"] // from Stripe dashboard
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

                case "checkout.session.completed":
                    var session = stripeEvent.Data.Object as Session;

                    var sessionId = session.Id;

                    var payment = _dbContext.TransferPaymentRequest
                        .FirstOrDefault(x => x.StripeSessionId == sessionId);

                    if (payment != null)
                    {
                        payment.PaymentStatus = "Succeeded";
                        payment.PaymentIntentId = session.PaymentIntentId;
                        payment.UpdatedDate = DateTime.UtcNow;

                        _dbContext.SaveChanges();
                    }
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

    [HttpPost("create-transfer-payment-link")]
    public IActionResult CreateTransferPaymentLink([FromBody] TransferPaymentRequest request)
{
    try
    {
            // ✅ Save payment in DB first
            var payment = new TransferPaymentRequest
            {
                Amount = request.Amount,
                Currency = request.Currency,
                DestinationAccountId = request.DestinationAccountId,
                PlatformFee = request.PlatformFee,
                ProductName = request.ProductName,
                OrderId = request.OrderId,
                CustomerEmail = request.CustomerEmail,
                PaymentStatus = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.TransferPaymentRequest.Add(payment);
            _dbContext.SaveChanges();


            var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "payment",

            SuccessUrl = _configuration["Stripe:PaymentSuccessUrl"],
            CancelUrl = _configuration["Stripe:PaymentCancelUrl"],

            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Quantity = 1,

                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = request.Amount,

                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.ProductName
                        }
                    }
                }
            },

            // 🔥 Transfer to connected account
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                TransferData = new SessionPaymentIntentDataTransferDataOptions
                {
                    Destination = request.DestinationAccountId
                },

                // Platform fee
                ApplicationFeeAmount = request.PlatformFee,

                Metadata = new Dictionary<string, string>
                {
                    { "orderId", request.OrderId }
                }
            }
        };

            var service = new SessionService();
            var session = service.Create(options);
            // ✅ Update DB with Stripe SessionId
            payment.StripeSessionId = session.Id;

            _dbContext.TransferPaymentRequest.Update(payment);
            _dbContext.SaveChanges();


            return Ok(new
        {
            paymentUrl = session.Url
        });
    }
    catch (StripeException ex)
    {
        return BadRequest(new
        {
            message = ex.Message
        });
    }
}

    [HttpGet("onboarding/success")]
    public IActionResult OnboardingSuccess(string accountId)
    {
        var service = new AccountService();
        var account = service.Get(accountId);

        if (account.DetailsSubmitted)
        {
            return Content("Onboarding Completed ✅");
        }

        return Content("Onboarding Pending ⏳");
    }

    [HttpGet("get-onboard-status/{accountId}")]
    public IActionResult GetOnboardStatus(string accountId)
    {
        try
        {
            var account = _dbContext.StripeAccounts
                .FirstOrDefault(x => x.StripeAccountId == accountId);

            if (account == null)
            {
                return NotFound(new
                {
                    message = "Account not found"
                });
            }

            return Ok(new
            {
                accountId = account.StripeAccountId,
                email = account.Email,
                detailsSubmitted = account.DetailsSubmitted,
                chargesEnabled = account.ChargesEnabled,
                payoutsEnabled = account.PayoutsEnabled,
                onboardingCompleted = account.OnboardingCompleted
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

}