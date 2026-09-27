using MongoDB.Driver;
using HotChocolate.Subscriptions;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.Messaging;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Services;

public interface IWebhookService
{
    Task<List<WebhookLog>> GetRecentAsync(string? integrationId, int limit);
    Task<WebhookLog> RecordEventAsync(WebhookLog log);
}

public class WebhookService : IWebhookService
{
    private readonly MongoDbContext _context;
    private readonly ITopicEventSender _eventSender;
    private readonly IKafkaProducerService _kafkaProducer;

    public WebhookService(MongoDbContext context, ITopicEventSender eventSender, IKafkaProducerService kafkaProducer)
    {
        _context = context;
        _eventSender = eventSender;
        _kafkaProducer = kafkaProducer;
    }

    public async Task<List<WebhookLog>> GetRecentAsync(string? integrationId, int limit)
    {
        var filter = string.IsNullOrEmpty(integrationId)
            ? Builders<WebhookLog>.Filter.Empty
            : Builders<WebhookLog>.Filter.Eq(w => w.IntegrationId, integrationId);

        return await _context.WebhookLogs.Find(filter)
            .SortByDescending(w => w.ReceivedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<WebhookLog> RecordEventAsync(WebhookLog log)
    {
        log.ReceivedAt = DateTime.UtcNow;
        await _context.WebhookLogs.InsertOneAsync(log);

        // Push to any connected GraphQL subscription clients for the live activity feed.
        await _eventSender.SendAsync("onWebhookEvent", log);

        // Publish onto Kafka so downstream services (EDI processor, notification/retry
        // service, analytics service) can react to the same event independently of the
        // Angular dashboard's GraphQL subscription.
        await _kafkaProducer.PublishAsync(KafkaTopics.WebhookEvents, log.IntegrationId, WebhookEventMessage.FromLog(log));

        return log;
    }
}
