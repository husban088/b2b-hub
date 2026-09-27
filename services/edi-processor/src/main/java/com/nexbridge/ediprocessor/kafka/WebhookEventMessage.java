package com.nexbridge.ediprocessor.kafka;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;

/**
 * Mirrors backend/Messaging/WebhookEventMessage.cs exactly. The .NET producer
 * serializes with JsonSerializerDefaults.Web (camelCase property names), and
 * Jackson's default bean-property naming matches these getter/setter pairs
 * one-for-one (getEventId/setEventId <-> "eventId", etc.) - no @JsonProperty
 * overrides needed. If a field is ever renamed on the .NET side, mirror it here.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
public class WebhookEventMessage {

    private String eventId;
    private String integrationId;
    private String direction;
    private String eventType;
    private int statusCode;
    private boolean success;
    private String payload;
    private String errorMessage;
    private String receivedAt;

    public String getEventId() { return eventId; }
    public void setEventId(String eventId) { this.eventId = eventId; }
    public String getIntegrationId() { return integrationId; }
    public void setIntegrationId(String integrationId) { this.integrationId = integrationId; }
    public String getDirection() { return direction; }
    public void setDirection(String direction) { this.direction = direction; }
    public String getEventType() { return eventType; }
    public void setEventType(String eventType) { this.eventType = eventType; }
    public int getStatusCode() { return statusCode; }
    public void setStatusCode(int statusCode) { this.statusCode = statusCode; }
    public boolean isSuccess() { return success; }
    public void setSuccess(boolean success) { this.success = success; }
    public String getPayload() { return payload; }
    public void setPayload(String payload) { this.payload = payload; }
    public String getErrorMessage() { return errorMessage; }
    public void setErrorMessage(String errorMessage) { this.errorMessage = errorMessage; }
    public String getReceivedAt() { return receivedAt; }
    public void setReceivedAt(String receivedAt) { this.receivedAt = receivedAt; }

    @Override
    public String toString() {
        return "WebhookEventMessage{eventId='%s', integrationId='%s', eventType='%s', success=%s}"
                .formatted(eventId, integrationId, eventType, success);
    }
}
