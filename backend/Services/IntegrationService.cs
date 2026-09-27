using MongoDB.Driver;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Services;

public interface IIntegrationService
{
    Task<List<Integration>> GetAllAsync();

    /// <summary>Only the integrations that belong to one of these partner IDs -
    /// used to scope the integrations list to the logged-in user's own company.</summary>
    Task<List<Integration>> GetAllForPartnersAsync(IReadOnlyCollection<string> partnerIds);

    Task<List<Integration>> GetByPartnerIdAsync(string partnerId);
    Task<Integration?> GetByIdAsync(string id);
    Task<Integration> CreateAsync(Integration integration);
    Task<Integration?> UpdateStatusAsync(string id, IntegrationStatus status);
    Task<bool> DeleteAsync(string id);
}

public class IntegrationService : IIntegrationService
{
    private readonly MongoDbContext _context;

    public IntegrationService(MongoDbContext context) => _context = context;

    public async Task<List<Integration>> GetAllAsync() =>
        await _context.Integrations.Find(_ => true).SortByDescending(i => i.CreatedAt).ToListAsync();

    public async Task<List<Integration>> GetAllForPartnersAsync(IReadOnlyCollection<string> partnerIds)
    {
        if (partnerIds.Count == 0) return new List<Integration>();

        return await _context.Integrations
            .Find(i => partnerIds.Contains(i.PartnerId))
            .SortByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Integration>> GetByPartnerIdAsync(string partnerId) =>
        await _context.Integrations.Find(i => i.PartnerId == partnerId).ToListAsync();

    public async Task<Integration?> GetByIdAsync(string id) =>
        await _context.Integrations.Find(i => i.Id == id).FirstOrDefaultAsync();

    public async Task<Integration> CreateAsync(Integration integration)
    {
        integration.CreatedAt = DateTime.UtcNow;
        integration.UpdatedAt = DateTime.UtcNow;
        await _context.Integrations.InsertOneAsync(integration);
        return integration;
    }

    public async Task<Integration?> UpdateStatusAsync(string id, IntegrationStatus status)
    {
        var update = Builders<Integration>.Update
            .Set(i => i.Status, status)
            .Set(i => i.UpdatedAt, DateTime.UtcNow)
            .Set(i => i.LastSyncAt, DateTime.UtcNow);

        var result = await _context.Integrations.UpdateOneAsync(i => i.Id == id, update);
        if (result.MatchedCount == 0) return null;

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _context.Integrations.DeleteOneAsync(i => i.Id == id);
        return result.DeletedCount > 0;
    }
}