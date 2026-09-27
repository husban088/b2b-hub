namespace B2BIntegrationHub.Messaging;

/// <summary>
/// Strongly typed binding for the "Kafka" section of appsettings.json / environment variables.
/// </summary>
public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";

    // Leave these empty for plain unauthenticated Kafka (local/Docker). Fill them in to
    // point at a hosted broker instead (e.g. Upstash Kafka's free tier) - useful when
    // running natively without installing Kafka.
    public string SecurityProtocol { get; set; } = "Plaintext"; // Plaintext | SaslSsl
    public string SaslMechanism { get; set; } = "Plain";        // Plain | ScramSha256 | ScramSha512
    public string SaslUsername { get; set; } = string.Empty;
    public string SaslPassword { get; set; } = string.Empty;
}
