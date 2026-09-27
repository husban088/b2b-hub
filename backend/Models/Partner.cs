using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2BIntegrationHub.Models;

public enum PartnerStatus
{
    Pending,
    Active,
    Suspended,
    Offboarded
}

/// <summary>
/// A business partner (client company) connected to the integration hub.
/// Belongs to exactly one Company (tenant) - BMW's clients are never visible to Audi.
/// </summary>
public class Partner
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>The tenant (Company) this client belongs to.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string CompanyId { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;

    /// <summary>Cloudinary secure URL for the partner's logo.</summary>
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }

    public PartnerStatus Status { get; set; } = PartnerStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
