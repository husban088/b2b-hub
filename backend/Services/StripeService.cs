using Microsoft.Extensions.Options;
using Stripe;

namespace B2BIntegrationHub.Services;

public class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public record PaymentLinkResult(string Id, string Url);

public interface IStripeService
{
    /// <summary>Creates a Stripe Payment Link for one invoice - the "Pay Now" button.</summary>
    Task<PaymentLinkResult> CreatePaymentLinkAsync(string description, decimal totalAmount, string currency, string invoiceId);
}

public class StripeService : IStripeService
{
    public StripeService(IOptions<StripeSettings> settings)
    {
        // Global Stripe.net config - every StripeClient call below uses this key.
        StripeConfiguration.ApiKey = settings.Value.SecretKey;
    }

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(
        string description, decimal totalAmount, string currency, string invoiceId)
    {
        // Stripe wants the amount in the smallest currency unit (cents for EUR).
        var unitAmount = (long)Math.Round(totalAmount * 100, MidpointRounding.AwayFromZero);

        var priceService = new PriceService();
        var price = await priceService.CreateAsync(new PriceCreateOptions
        {
            Currency = currency,
            UnitAmount = unitAmount,
            ProductData = new PriceProductDataOptions { Name = description }
        });

        var linkService = new PaymentLinkService();
        var link = await linkService.CreateAsync(new PaymentLinkCreateOptions
        {
            LineItems = new List<PaymentLinkLineItemOptions>
            {
                new() { Price = price.Id, Quantity = 1 }
            },
            Metadata = new Dictionary<string, string> { { "invoiceId", invoiceId } }
        });

        return new PaymentLinkResult(link.Id, link.Url);
    }
}
