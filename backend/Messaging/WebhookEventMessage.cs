using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Messaging;

/// <summary>
/// Stable JSON contract published to the "webhook.events.v1" Kafka topic.
/// This is a public API between services, not an internal DTO - if you rename
/// or remove a field here, every downstream consumer (Java/Kotlin/Python)
/// breaks silently. Add new fields freely; avoid renaming/removing existing ones.
/// </summary>
public class WebhookEventMessage
{
    public string EventId { get; set; } = string.Empty;
    public string IntegrationId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // inbound | outbound
    public string EventType { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? Payload { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; }

    public static WebhookEventMessage FromLog(WebhookLog log) => new()
    {
        EventId = log.Id,
        IntegrationId = log.IntegrationId,
        Direction = log.Direction,
        EventType = log.EventType,
        StatusCode = log.StatusCode,
        Success = log.Success,
        Payload = log.Payload,
        ErrorMessage = log.ErrorMessage,
        ReceivedAt = log.ReceivedAt
    };
}
