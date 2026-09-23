using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2BIntegrationHub.Models;

public enum IntegrationType
{
    RestApi,
    Graphql,
    Webhook,
    FileSync,
    Edi
}

public enum IntegrationStatus
{
    Draft,
    Connected,
    Failing,
    Paused
}

/// <summary>
/// One connected integration endpoint that belongs to a Partner
/// (e.g. their order-sync REST API, their invoicing webhook, etc).
/// </summary>
public class Integration
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string PartnerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public IntegrationType Type { get; set; }
    public IntegrationStatus Status { get; set; } = IntegrationStatus.Draft;

    /// <summary>Base URL of the partner's endpoint we call, or the URL we expose to them.</summary>
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>Hashed/opaque reference to the credential; never store raw secrets in Mongo.</summary>
    public string ApiKeyReference { get; set; } = string.Empty;

    public string[] Scopes { get; set; } = Array.Empty<string>();

    public DateTime? LastSyncAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
