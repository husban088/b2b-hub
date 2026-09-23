using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using B2BIntegrationHub.Models;
using B2BIntegrationHub.Services;

namespace B2BIntegrationHub.Controllers;

// Payload can be any JSON (object, array, string) - partners rarely send it as a pre-stringified value.
public record IncomingWebhookDto(string EventType, JsonElement? Payload);

/// <summary>
/// Plain REST endpoint partners can point their existing webhook senders at,
/// so they don't need a GraphQL client just to push events into the hub.
/// Every accepted event is fanned out to GraphQL subscribers in real time.
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class WebhookReceiverController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly IIntegrationService _integrationService;

    public WebhookReceiverController(IWebhookService webhookService, IIntegrationService integrationService)
    {
        _webhookService = webhookService;
        _integrationService = integrationService;
    }

    [HttpPost("{integrationId}")]
    public async Task<IActionResult> Receive(string integrationId, [FromBody] IncomingWebhookDto dto)
    {
        // A malformed id would otherwise crash the Mongo query with a 500.
        if (!ObjectId.TryParse(integrationId, out _))
        {
            return NotFound(new { message = "Unknown integration id." });
        }

        var integration = await _integrationService.GetByIdAsync(integrationId);
        if (integration is null)
        {
            return NotFound(new { message = "Unknown integration id." });
        }

        string? payload = null;
        if (dto.Payload is { } p && p.ValueKind != JsonValueKind.Null && p.ValueKind != JsonValueKind.Undefined)
        {
            payload = p.ValueKind == JsonValueKind.String ? p.GetString() : p.GetRawText();
        }

        var log = await _webhookService.RecordEventAsync(new WebhookLog
        {
            IntegrationId = integrationId,
            Direction = "inbound",
            EventType = dto.EventType,
            StatusCode = 200,
            Success = true,
            Payload = payload
        });

        await _integrationService.UpdateStatusAsync(integrationId, IntegrationStatus.Connected);

        return Ok(new { received = true, logId = log.Id });
    }
}