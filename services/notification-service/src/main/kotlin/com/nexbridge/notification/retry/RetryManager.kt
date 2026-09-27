package com.nexbridge.notification.retry

import com.nexbridge.notification.alert.AlertService
import com.nexbridge.notification.models.IntegrationDoc
import com.nexbridge.notification.models.WebhookEventMessage
import com.nexbridge.notification.mongo.MongoClientProvider
import io.ktor.client.*
import io.ktor.client.engine.cio.*
import io.ktor.client.request.*
import io.ktor.http.*
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.firstOrNull
import kotlinx.coroutines.launch
import org.bson.Document
import org.bson.types.ObjectId
import org.slf4j.LoggerFactory
import java.time.Instant

/**
 * Owns two things:
 *  1. The consecutive-failure counter per integration (so AlertService knows when to fire).
 *  2. Actually retrying a failed *outbound* webhook delivery, with exponential backoff.
 *
 * Only "outbound" events are retried - "inbound" events are webhooks partners sent *to* us;
 * there is nothing for us to resend on their behalf.
 */
class RetryManager(private val scope: CoroutineScope) {
    private val log = LoggerFactory.getLogger(RetryManager::class.java)
    private val failureState = MongoClientProvider.database.getCollection<Document>("integration_failure_state")
    private val integrations = MongoClientProvider.database.getCollection<Document>("integrations")
    private val httpClient = HttpClient(CIO)

    private val alertThreshold = (System.getenv("NOTIFICATION_ALERT_THRESHOLD") ?: "3").toInt()
    private val maxRetryAttempts = (System.getenv("NOTIFICATION_MAX_RETRY_ATTEMPTS") ?: "3").toInt()
    private val backoffBaseMs = (System.getenv("NOTIFICATION_RETRY_BACKOFF_MS") ?: "2000").toLong()

    suspend fun handle(event: WebhookEventMessage) {
        if (event.direction != "outbound") return // nothing to retry/alert on for inbound events

        if (event.success) {
            resetFailureStreak(event.integrationId)
            return
        }

        val consecutiveFailures = recordFailure(event.integrationId, event.errorMessage)

        if (consecutiveFailures >= alertThreshold && !AlertService.hasOpenAlert(event.integrationId)) {
            val partnerId = fetchIntegration(event.integrationId)?.partnerId ?: "unknown"
            AlertService.fire(event.integrationId, partnerId, consecutiveFailures, event.errorMessage)
        }

        // Retrying is fire-and-forget from the consumer loop's point of view - we don't want
        // a slow/backed-off retry to block reading the next Kafka message.
        scope.launch { retryWithBackoff(event) }
    }

    private suspend fun retryWithBackoff(event: WebhookEventMessage) {
        val integration = fetchIntegration(event.integrationId)
        if (integration == null || integration.endpointUrl.isBlank()) {
            log.warn("Cannot retry event {} - integration {} has no endpointUrl on file.", event.eventId, event.integrationId)
            return
        }

        for (attempt in 1..maxRetryAttempts) {
            val delayMs = backoffBaseMs * (1L shl (attempt - 1)) // 2s, 4s, 8s, ...
            delay(delayMs)

            val ok = tryDeliver(integration.endpointUrl, event)
            if (ok) {
                log.info("Retry succeeded for event {} (integration {}) on attempt {}.", event.eventId, event.integrationId, attempt)
                resetFailureStreak(event.integrationId)
                return
            }
            log.warn("Retry attempt {}/{} failed for event {} (integration {}).", attempt, maxRetryAttempts, event.eventId, event.integrationId)
        }

        log.warn("Giving up on event {} (integration {}) after {} retry attempts.", event.eventId, event.integrationId, maxRetryAttempts)
    }

    private suspend fun tryDeliver(endpointUrl: String, event: WebhookEventMessage): Boolean =
        try {
            val response = httpClient.post(endpointUrl) {
                contentType(ContentType.Application.Json)
                setBody(event.payload ?: "{}")
            }
            response.status.value in 200..299
        } catch (ex: Exception) {
            log.debug("Delivery attempt threw for event {}: {}", event.eventId, ex.message)
            false
        }

    private suspend fun recordFailure(integrationId: String, error: String?): Int {
        val existing = failureState.find(Document("integrationId", integrationId)).firstOrNull()
        val newCount = (existing?.getInteger("consecutiveFailures") ?: 0) + 1

        failureState.replaceOne(
            Document("integrationId", integrationId),
            Document()
                .append("integrationId", integrationId)
                .append("consecutiveFailures", newCount)
                .append("lastError", error)
                .append("lastFailureAt", Instant.now().toString()),
            com.mongodb.client.model.ReplaceOptions().upsert(true)
        )
        return newCount
    }

    private suspend fun resetFailureStreak(integrationId: String) {
        failureState.replaceOne(
            Document("integrationId", integrationId),
            Document()
                .append("integrationId", integrationId)
                .append("consecutiveFailures", 0)
                .append("lastError", null)
                .append("lastFailureAt", null),
            com.mongodb.client.model.ReplaceOptions().upsert(true)
        )
        // Any open alert for this integration is now stale - clear it so a fresh streak can alert again.
        MongoClientProvider.database.getCollection<Document>("integration_alerts").updateMany(
            Document("integrationId", integrationId).append("acknowledged", false),
            Document("\$set", Document("acknowledged", true))
        )
    }

    private suspend fun fetchIntegration(integrationId: String): IntegrationDoc? {
        val id = runCatching { ObjectId(integrationId) }.getOrNull() ?: return null
        val doc = integrations.find(Document("_id", id)).firstOrNull() ?: return null
        return IntegrationDoc(
            _id = doc.getObjectId("_id"),
            partnerId = doc.getString("PartnerId") ?: doc.getString("partnerId") ?: "",
            name = doc.getString("Name") ?: doc.getString("name") ?: "",
            type = doc.get("Type")?.toString() ?: "",
            status = doc.get("Status")?.toString() ?: "",
            endpointUrl = doc.getString("EndpointUrl") ?: doc.getString("endpointUrl") ?: "",
            apiKeyReference = doc.getString("ApiKeyReference") ?: doc.getString("apiKeyReference") ?: ""
        )
    }
}
