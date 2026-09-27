using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using B2BIntegrationHub.Services;

namespace B2BIntegrationHub.Controllers;

/// <summary>
/// Stripe calls this the instant a client actually pays a "Pay Now" link.
/// This is the trigger for HISSA 5: PENDING -> PAID + the live dashboard update.
/// </summary>
[ApiController]
[Route("api/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly IInvoiceService _invoices;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IInvoiceService invoices, IOptions<StripeSettings> settings, ILogger<StripeWebhookController> logger)
    {
        _invoices = invoices;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        Event stripeEvent;
        try
        {
            // Verifies the request really came from Stripe using the signing secret
            // (Stripe Dashboard -> Developers -> Webhooks -> your endpoint -> Signing secret).
            stripeEvent = EventUtility.ConstructEvent(
                json, Request.Headers["Stripe-Signature"], _settings.WebhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return BadRequest();
        }

        // A Payment Link checkout finishing is reported as checkout.session.completed,
        // with session.PaymentLink pointing back at the link we created for the invoice.
        if (stripeEvent.Type == "checkout.session.completed")
        {
            var session = stripeEvent.Data.Object as Session;
            var paymentLinkId = session?.PaymentLink?.Id;

            if (!string.IsNullOrEmpty(paymentLinkId))
            {
                var invoice = await _invoices.MarkPaidByPaymentLinkAsync(paymentLinkId);
                if (invoice is null)
                {
                    _logger.LogWarning("Stripe payment link {LinkId} did not match any invoice.", paymentLinkId);
                }
            }
        }

        return Ok();
    }
}