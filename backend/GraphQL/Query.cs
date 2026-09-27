using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using B2BIntegrationHub.Models;
using B2BIntegrationHub.Services;

namespace B2BIntegrationHub.GraphQL;

public class Query
{
    [Authorize]
    public Task<List<Partner>> GetPartners([Service] IPartnerService service, [Service] ICurrentUserService currentUser) =>
        service.GetAllAsync(RequireCompanyId(currentUser));

    [Authorize]
    public Task<Partner?> GetPartner(string id, [Service] IPartnerService service, [Service] ICurrentUserService currentUser) =>
        service.GetByIdAsync(id, RequireCompanyId(currentUser));

    /// <summary>The logged-in user's own company - name, VAT number, IBAN, etc.</summary>
    [Authorize]
    public Task<Company?> GetMyCompany([Service] ICompanyService service, [Service] ICurrentUserService currentUser) =>
        service.GetByIdAsync(RequireCompanyId(currentUser));

    /// <summary>Only integrations belonging to a partner in the logged-in user's own company.</summary>
    [Authorize]
    public async Task<List<Integration>> GetIntegrations(
        [Service] IIntegrationService integrations,
        [Service] IPartnerService partners,
        [Service] ICurrentUserService currentUser)
    {
        var companyId = RequireCompanyId(currentUser);
        var myPartnerIds = (await partners.GetAllAsync(companyId)).Select(p => p.Id).ToList();
        return await integrations.GetAllForPartnersAsync(myPartnerIds);
    }

    public Task<List<Integration>> GetIntegrationsByPartner(string partnerId, [Service] IIntegrationService service) =>
        service.GetByPartnerIdAsync(partnerId);

    public Task<Integration?> GetIntegration(string id, [Service] IIntegrationService service) =>
        service.GetByIdAsync(id);

    public Task<List<WebhookLog>> GetWebhookLogs(
        [Service] IWebhookService service, string? integrationId = null, int limit = 50) =>
        service.GetRecentAsync(integrationId, limit);

    /// <summary>Lightweight aggregate used by the dashboard's summary tiles (this company only).</summary>
    [Authorize]
    public async Task<DashboardSummary> GetDashboardSummary(
        [Service] IPartnerService partners,
        [Service] IIntegrationService integrations,
        [Service] IWebhookService webhooks,
        [Service] ICurrentUserService currentUser)
    {
        var companyId = RequireCompanyId(currentUser);
        var allPartners = await partners.GetAllAsync(companyId);
        var myPartnerIds = allPartners.Select(p => p.Id).ToList();
        var allIntegrations = await integrations.GetAllForPartnersAsync(myPartnerIds);
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

    [Authorize]
    public Task<List<Invoice>> GetInvoices([Service] IInvoiceService service, [Service] ICurrentUserService currentUser) =>
        service.GetAllAsync(RequireCompanyId(currentUser));

    [Authorize]
    public Task<Invoice?> GetInvoice(string id, [Service] IInvoiceService service, [Service] ICurrentUserService currentUser) =>
        service.GetByIdAsync(id, RequireCompanyId(currentUser));

    private static string RequireCompanyId(ICurrentUserService currentUser) =>
        currentUser.CompanyId ?? throw new GraphQLException("Not logged into a company.");
}

public record DashboardSummary(
    int TotalPartners,
    int ActivePartners,
    int TotalIntegrations,
    int ConnectedIntegrations,
    int FailingIntegrations,
    int RecentEvents);