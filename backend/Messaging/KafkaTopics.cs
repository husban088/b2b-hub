namespace B2BIntegrationHub.Messaging;

/// <summary>
/// Central registry of Kafka topic names. Every downstream consumer service
/// (Java EDI processor, Kotlin notification/retry service, Python analytics
/// service) reads from these same topic names - treat this file as the
/// single source of truth so nobody hardcodes a topic string elsewhere.
///
/// Naming convention: "{domain}.{event}.v{contractVersion}". Bump the version
/// suffix (v2, v3, ...) instead of changing a live topic's message shape, so
/// old consumers don't silently break.
/// </summary>
public static class KafkaTopics
{
    /// <summary>
    /// Every accepted webhook event (inbound or outbound), published the moment
    /// it's recorded - same event that drives the GraphQL live activity feed.
    /// Message contract: <see cref="WebhookEventMessage"/>.
    /// </summary>
    public const string WebhookEvents = "webhook.events.v1";
}
