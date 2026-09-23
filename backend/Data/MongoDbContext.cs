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
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<Partner> Partners => _database.GetCollection<Partner>("partners");
    public IMongoCollection<Integration> Integrations => _database.GetCollection<Integration>("integrations");
    public IMongoCollection<WebhookLog> WebhookLogs => _database.GetCollection<WebhookLog>("webhook_logs");
    public IMongoCollection<AppUser> Users => _database.GetCollection<AppUser>("users");

    /// <summary>
    /// Creates indexes the first time the API boots. Safe to call repeatedly (idempotent).
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        var partnerIndex = new CreateIndexModel<Partner>(
            Builders<Partner>.IndexKeys.Ascending(p => p.CompanyEmail), new CreateIndexOptions { Unique = true });
        await Partners.Indexes.CreateOneAsync(partnerIndex);

        var userIndex = new CreateIndexModel<AppUser>(
            Builders<AppUser>.IndexKeys.Ascending(u => u.Email), new CreateIndexOptions { Unique = true });
        await Users.Indexes.CreateOneAsync(userIndex);

        var integrationIndex = new CreateIndexModel<Integration>(
            Builders<Integration>.IndexKeys.Ascending(i => i.PartnerId));
        await Integrations.Indexes.CreateOneAsync(integrationIndex);

        var webhookLogIndex = new CreateIndexModel<WebhookLog>(
            Builders<WebhookLog>.IndexKeys.Descending(w => w.ReceivedAt));
        await WebhookLogs.Indexes.CreateOneAsync(webhookLogIndex);
    }
}
