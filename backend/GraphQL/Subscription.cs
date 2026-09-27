using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using B2BIntegrationHub.Models;
using B2BIntegrationHub.Services;

namespace B2BIntegrationHub.GraphQL;

/// <summary>
/// Real-time channels the Angular dashboard subscribes to.
/// </summary>
public class Subscription
{
    [Subscribe]
    [Topic("onWebhookEvent")]
    public WebhookLog OnWebhookEvent([EventMessage] WebhookLog log) => log;

    /// <summary>
    /// Fires the instant a client pays an invoice (Stripe webhook -> here). Topic is scoped
    /// to the logged-in user's own CompanyId, so one company never sees another's payments.
    /// </summary>
    [SubscribeAndResolve]
    public async ValueTask<ISourceStream<Invoice>> OnInvoicePaid(
        [Service] ITopicEventReceiver receiver, [Service] ICurrentUserService currentUser)
    {
        var companyId = currentUser.CompanyId ?? throw new GraphQLException("Not logged into a company.");
        return await receiver.SubscribeAsync<Invoice>($"onInvoicePaid_{companyId}");
    }
}