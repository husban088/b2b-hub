using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2BIntegrationHub.Models;

/// <summary>
/// The company (tenant) that owns its own users, clients (Partners) and invoices.
/// Every record that belongs to a company carries its CompanyId, so one company
/// never sees another company's data (multi-tenant isolation).
/// </summary>
public class Company
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Name { get; set; } = string.Empty;

    /// <summary>German VAT number, e.g. DE123456789 - required on every invoice.</summary>
    public string VatNumber { get; set; } = string.Empty;

    /// <summary>IBAN the company gets paid into - shown on the invoice PDF.</summary>
    public string Iban { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = "DE";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
