using MongoDB.Driver;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Services;

public interface IPartnerService
{
    Task<List<Partner>> GetAllAsync();
    Task<Partner?> GetByIdAsync(string id);
    Task<Partner> CreateAsync(Partner partner);
    Task<Partner?> UpdateAsync(string id, Partner partner);
    Task<bool> DeleteAsync(string id);
}

public class PartnerService : IPartnerService
{
    private readonly MongoDbContext _context;

    public PartnerService(MongoDbContext context) => _context = context;

    public async Task<List<Partner>> GetAllAsync() =>
        await _context.Partners.Find(_ => true).SortByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<Partner?> GetByIdAsync(string id) =>
        await _context.Partners.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<Partner> CreateAsync(Partner partner)
    {
        partner.CreatedAt = DateTime.UtcNow;
        partner.UpdatedAt = DateTime.UtcNow;
        await _context.Partners.InsertOneAsync(partner);
        return partner;
    }

    public async Task<Partner?> UpdateAsync(string id, Partner partner)
    {
        partner.Id = id;
        partner.UpdatedAt = DateTime.UtcNow;

        // ReplaceOneAsync avoids the ambiguous FindOneAndReplaceAsync overload error (CS0121).
        var result = await _context.Partners.ReplaceOneAsync(p => p.Id == id, partner);
        return result.MatchedCount > 0 ? partner : null;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _context.Partners.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }
}