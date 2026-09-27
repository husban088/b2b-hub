package com.nexbridge.notification.alert

import com.nexbridge.notification.mongo.MongoClientProvider
import kotlinx.coroutines.flow.firstOrNull
import org.bson.Document
import org.slf4j.LoggerFactory
import java.time.Instant

/**
 * Fires when an integration's outbound webhook has failed N times in a row
 * (threshold set by NOTIFICATION_ALERT_THRESHOLD, default 3). For now "sending
 * an alert" means: write a doc the Angular dashboard can query/subscribe to,
 * plus a WARN log line. Swap in real email/Slack delivery later without
 * touching the caller (RetryManager) - it only ever calls fire().
 */
object AlertService {
    private val log = LoggerFactory.getLogger(AlertService::class.java)
    private val alerts = MongoClientProvider.database.getCollection<Document>("integration_alerts")

    suspend fun fire(integrationId: String, partnerId: String, consecutiveFailures: Int, lastError: String?) {
        log.warn(
            "ALERT: integration {} (partner {}) has failed {} times in a row. Last error: {}",
            integrationId, partnerId, consecutiveFailures, lastError ?: "unknown"
        )

        val doc = Document()
            .append("integrationId", integrationId)
            .append("partnerId", partnerId)
            .append("consecutiveFailures", consecutiveFailures)
            .append("lastError", lastError)
            .append("firedAt", Instant.now().toString())
            .append("acknowledged", false)

        alerts.insertOne(doc)
    }

    /** True if there is already an unacknowledged alert open for this integration's current failure streak. */
    suspend fun hasOpenAlert(integrationId: String): Boolean =
        alerts.find(Document("integrationId", integrationId).append("acknowledged", false))
            .firstOrNull() != null
}
