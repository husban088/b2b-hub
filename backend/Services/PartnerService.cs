using MongoDB.Driver;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Services;

public interface IPartnerService
{
    Task<List<Partner>> GetAllAsync(string companyId);
    Task<Partner?> GetByIdAsync(string id, string companyId);
    Task<Partner> CreateAsync(Partner partner);
    Task<Partner?> UpdateAsync(string id, string companyId, Partner partner);
    Task<bool> DeleteAsync(string id, string companyId);
}

public class PartnerService : IPartnerService
{
    private readonly MongoDbContext _context;

    public PartnerService(MongoDbContext context) => _context = context;

    // companyId filter on every read is what stops BMW's login from ever seeing Audi's clients.
    public async Task<List<Partner>> GetAllAsync(string companyId) =>
        await _context.Partners.Find(p => p.CompanyId == companyId)
            .SortByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<Partner?> GetByIdAsync(string id, string companyId) =>
        await _context.Partners.Find(p => p.Id == id && p.CompanyId == companyId).FirstOrDefaultAsync();

    public async Task<Partner> CreateAsync(Partner partner)
    {
        partner.CreatedAt = DateTime.UtcNow;
        partner.UpdatedAt = DateTime.UtcNow;
        await _context.Partners.InsertOneAsync(partner);
        return partner;
    }

    public async Task<Partner?> UpdateAsync(string id, string companyId, Partner partner)
    {
        partner.Id = id;
        partner.CompanyId = companyId;
        partner.UpdatedAt = DateTime.UtcNow;

        // Filtering by companyId here too: you can never update another company's client.
        var result = await _context.Partners.ReplaceOneAsync(
            p => p.Id == id && p.CompanyId == companyId, partner);
        return result.MatchedCount > 0 ? partner : null;
    }

    public async Task<bool> DeleteAsync(string id, string companyId)
    {
        var result = await _context.Partners.DeleteOneAsync(p => p.Id == id && p.CompanyId == companyId);
        return result.DeletedCount > 0;
    }
}
