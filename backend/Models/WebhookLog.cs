using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2BIntegrationHub.Models;

/// <summary>
/// Immutable audit record of every inbound/outbound webhook event moving
/// through an integration. Used for the live activity feed and debugging.
/// </summary>
public class WebhookLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string IntegrationId { get; set; } = string.Empty;

    public string Direction { get; set; } = "inbound"; // inbound | outbound
    public string EventType { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? Payload { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
