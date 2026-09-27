using Microsoft.Extensions.Options;
using MongoDB.Driver;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Data;

/// <summary>
/// Single entry point to every MongoDB collection used by the platform.
/// Registered as a singleton in Program.cs.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var mongoSettings = MongoClientSettings.FromConnectionString(settings.Value.ConnectionString);
        // Fail fast (10s instead of the 30s default) so a bad connection string / blocked IP
        // shows up as a quick error instead of a hanging request.
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        var client = new MongoClient(mongoSettings);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<Company> Companies => _database.GetCollection<Company>("companies");
    public IMongoCollection<Partner> Partners => _database.GetCollection<Partner>("partners");
    public IMongoCollection<Integration> Integrations => _database.GetCollection<Integration>("integrations");
    public IMongoCollection<WebhookLog> WebhookLogs => _database.GetCollection<WebhookLog>("webhook_logs");
    public IMongoCollection<AppUser> Users => _database.GetCollection<AppUser>("users");
    public IMongoCollection<Invoice> Invoices => _database.GetCollection<Invoice>("invoices");

    /// <summary>
    /// Creates indexes the first time the API boots. Safe to call repeatedly (idempotent).
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        // A client's email only needs to be unique *within* its own company now -
        // two different companies (tenants) are allowed to each have a client with the same email.
        var partnerIndex = new CreateIndexModel<Partner>(
            Builders<Partner>.IndexKeys.Ascending(p => p.CompanyId).Ascending(p => p.CompanyEmail),
            new CreateIndexOptions { Unique = true });
        await Partners.Indexes.CreateOneAsync(partnerIndex);

        var partnerCompanyIndex = new CreateIndexModel<Partner>(
            Builders<Partner>.IndexKeys.Ascending(p => p.CompanyId));
        await Partners.Indexes.CreateOneAsync(partnerCompanyIndex);

        var userIndex = new CreateIndexModel<AppUser>(
            Builders<AppUser>.IndexKeys.Ascending(u => u.Email), new CreateIndexOptions { Unique = true });
        await Users.Indexes.CreateOneAsync(userIndex);

        var userCompanyIndex = new CreateIndexModel<AppUser>(
            Builders<AppUser>.IndexKeys.Ascending(u => u.CompanyId));
        await Users.Indexes.CreateOneAsync(userCompanyIndex);

        var integrationIndex = new CreateIndexModel<Integration>(
            Builders<Integration>.IndexKeys.Ascending(i => i.PartnerId));
        await Integrations.Indexes.CreateOneAsync(integrationIndex);

        var webhookLogIndex = new CreateIndexModel<WebhookLog>(
            Builders<WebhookLog>.IndexKeys.Descending(w => w.ReceivedAt));
        await WebhookLogs.Indexes.CreateOneAsync(webhookLogIndex);

        var invoiceCompanyIndex = new CreateIndexModel<Invoice>(
            Builders<Invoice>.IndexKeys.Ascending(i => i.CompanyId).Descending(i => i.CreatedAt));
        await Invoices.Indexes.CreateOneAsync(invoiceCompanyIndex);

        // The Stripe webhook looks an invoice up by its payment link id, so this must be fast.
        var invoicePaymentLinkIndex = new CreateIndexModel<Invoice>(
            Builders<Invoice>.IndexKeys.Ascending(i => i.StripePaymentLinkId));
        await Invoices.Indexes.CreateOneAsync(invoicePaymentLinkIndex);
    }
}
