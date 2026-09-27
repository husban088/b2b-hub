using MongoDB.Driver;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Services;

public interface ICompanyService
{
    Task<Company> CreateAsync(Company company);
    Task<Company?> GetByIdAsync(string id);
    Task<Company?> UpdateAsync(string id, Company company);
}

public class CompanyService : ICompanyService
{
    private readonly MongoDbContext _context;

    public CompanyService(MongoDbContext context) => _context = context;

    public async Task<Company> CreateAsync(Company company)
    {
        company.CreatedAt = DateTime.UtcNow;
        company.UpdatedAt = DateTime.UtcNow;
        await _context.Companies.InsertOneAsync(company);
        return company;
    }

    public async Task<Company?> GetByIdAsync(string id) =>
        await _context.Companies.Find(c => c.Id == id).FirstOrDefaultAsync();

    public async Task<Company?> UpdateAsync(string id, Company company)
    {
        company.Id = id;
        company.UpdatedAt = DateTime.UtcNow;

        var result = await _context.Companies.ReplaceOneAsync(c => c.Id == id, company);
        return result.MatchedCount > 0 ? company : null;
    }
}
