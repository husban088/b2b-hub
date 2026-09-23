using HotChocolate;
using HotChocolate.Types;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.GraphQL;

/// <summary>
/// Real-time channel the Angular dashboard subscribes to for a live
/// "activity feed" of webhook events across every integration.
/// </summary>
public class Subscription
{
    [Subscribe]
    [Topic("onWebhookEvent")]
    public WebhookLog OnWebhookEvent([EventMessage] WebhookLog log) => log;
}