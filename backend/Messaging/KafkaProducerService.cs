using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace B2BIntegrationHub.Messaging;

public interface IKafkaProducerService
{
    Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin wrapper around Confluent.Kafka's IProducer. Registered as a singleton
/// because the underlying producer is thread-safe and expensive to create.
///
/// Design choice: publishing is best-effort. Mongo + the GraphQL subscription
/// push (see WebhookService) are the source of truth and already succeed
/// before this runs, so if Kafka is down/unreachable we log a warning and
/// move on instead of failing the webhook request. Downstream Kafka consumers
/// (EDI processor, notification service, analytics service) are an additional
/// stream, not the primary path.
/// </summary>
public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public KafkaProducerService(IOptions<KafkaSettings> options, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        var settings = options.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            MessageTimeoutMs = 5000,
            ClientId = "nexbridge-backend"
        };

        if (!string.Equals(settings.SecurityProtocol, "Plaintext", StringComparison.OrdinalIgnoreCase))
        {
            config.SecurityProtocol = Enum.Parse<SecurityProtocol>(settings.SecurityProtocol, ignoreCase: true);
            config.SaslMechanism = Enum.Parse<SaslMechanism>(settings.SaslMechanism, ignoreCase: true);
            config.SaslUsername = settings.SaslUsername;
            config.SaslPassword = settings.SaslPassword;
        }

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
                _logger.LogWarning("Kafka producer error: {Reason} (fatal: {IsFatal})", e.Reason, e.IsFatal))
            .Build();
    }

    public async Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            var result = await _producer.ProduceAsync(
                topic,
                new Message<string, string> { Key = key, Value = json },
                cancellationToken);

            _logger.LogDebug(
                "Published to {Topic} partition {Partition} @ offset {Offset}",
                topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish event to Kafka topic {Topic} - continuing without it (Mongo + live feed already succeeded).",
                topic);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error publishing to Kafka topic {Topic}.", topic);
        }
    }

    public void Dispose()
    {
        // Give in-flight messages a chance to actually leave the process on shutdown.
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
