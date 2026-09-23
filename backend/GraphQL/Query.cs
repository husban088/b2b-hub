using HotChocolate;
using HotChocolate.Types;
using B2BIntegrationHub.Models;
using B2BIntegrationHub.Services;

namespace B2BIntegrationHub.GraphQL;

public class Query
{
    public Task<List<Partner>> GetPartners([Service] IPartnerService service) =>
        service.GetAllAsync();

    public Task<Partner?> GetPartner(string id, [Service] IPartnerService service) =>
        service.GetByIdAsync(id);

    public Task<List<Integration>> GetIntegrations([Service] IIntegrationService service) =>
        service.GetAllAsync();

    public Task<List<Integration>> GetIntegrationsByPartner(string partnerId, [Service] IIntegrationService service) =>
        service.GetByPartnerIdAsync(partnerId);

    public Task<Integration?> GetIntegration(string id, [Service] IIntegrationService service) =>
        service.GetByIdAsync(id);

    public Task<List<WebhookLog>> GetWebhookLogs(
        [Service] IWebhookService service, string? integrationId = null, int limit = 50) =>
        service.GetRecentAsync(integrationId, limit);

    /// <summary>Lightweight aggregate used by the dashboard's summary tiles.</summary>
    public async Task<DashboardSummary> GetDashboardSummary(
        [Service] IPartnerService partners,
        [Service] IIntegrationService integrations,
        [Service] IWebhookService webhooks)
    {
        var allPartners = await partners.GetAllAsync();
        var allIntegrations = await integrations.GetAllAsync();
        var recentLogs = await webhooks.GetRecentAsync(null, 20);

        return new DashboardSummary(
            TotalPartners: allPartners.Count,
            ActivePartners: allPartners.Count(p => p.Status == PartnerStatus.Active),
            TotalIntegrations: allIntegrations.Count,
            ConnectedIntegrations: allIntegrations.Count(i => i.Status == IntegrationStatus.Connected),
            FailingIntegrations: allIntegrations.Count(i => i.Status == IntegrationStatus.Failing),
            RecentEvents: recentLogs.Count
        );
    }
}

public record DashboardSummary(
    int TotalPartners,
    int ActivePartners,
    int TotalIntegrations,
    int ConnectedIntegrations,
    int FailingIntegrations,
    int RecentEvents);