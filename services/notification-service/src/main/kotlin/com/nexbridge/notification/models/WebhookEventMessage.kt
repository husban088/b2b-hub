package com.nexbridge.notification.models

import com.fasterxml.jackson.annotation.JsonIgnoreProperties
import java.time.Instant

/**
 * Mirrors backend/Messaging/WebhookEventMessage.cs exactly (same field names as the JSON
 * the .NET backend publishes to "webhook.events.v1"). This is a public contract shared by
 * every consumer (Java EDI processor, this Kotlin service, the Python analytics service) -
 * do not rename fields here without renaming them on the producer side too.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
data class WebhookEventMessage(
    val eventId: String = "",
    val integrationId: String = "",
    val direction: String = "", // "inbound" | "outbound"
    val eventType: String = "",
    val statusCode: Int = 0,
    val success: Boolean = false,
    val payload: String? = null,
    val errorMessage: String? = null,
    val receivedAt: Instant = Instant.now()
)
