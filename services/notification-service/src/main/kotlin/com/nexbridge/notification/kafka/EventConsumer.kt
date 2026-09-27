package com.nexbridge.notification.kafka

import com.fasterxml.jackson.databind.ObjectMapper
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule
import com.fasterxml.jackson.module.kotlin.registerKotlinModule
import com.nexbridge.notification.models.WebhookEventMessage
import com.nexbridge.notification.retry.RetryManager
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.isActive
import kotlinx.coroutines.withContext
import org.apache.kafka.clients.consumer.ConsumerConfig
import org.apache.kafka.clients.consumer.KafkaConsumer
import org.apache.kafka.common.serialization.StringDeserializer
import org.slf4j.LoggerFactory
import java.time.Duration
import java.util.Properties

/**
 * Consumes "webhook.events.v1" the same way the Java EDI processor does (its own consumer
 * group, so both services independently see every message - Kafka fans out per group, not
 * per message). Every deserialized event is handed to RetryManager, which decides whether
 * it needs retrying/alerting; this class only owns the poll loop and JSON parsing.
 */
class EventConsumer(private val scope: CoroutineScope) {
    private val log = LoggerFactory.getLogger(EventConsumer::class.java)
    private val topic = System.getenv("NOTIFICATION_KAFKA_TOPIC") ?: "webhook.events.v1"
    private val bootstrapServers = System.getenv("KAFKA_BOOTSTRAP_SERVERS") ?: "localhost:9092"
    private val consumerGroup = System.getenv("NOTIFICATION_CONSUMER_GROUP") ?: "notification-service"

    private val mapper = ObjectMapper().apply {
        registerKotlinModule()
        registerModule(JavaTimeModule())
    }

    private val retryManager = RetryManager(scope)

    suspend fun run() = withContext(Dispatchers.IO) {
        val props = Properties().apply {
            put(ConsumerConfig.BOOTSTRAP_SERVERS_CONFIG, bootstrapServers)
            put(ConsumerConfig.GROUP_ID_CONFIG, consumerGroup)
            put(ConsumerConfig.KEY_DESERIALIZER_CLASS_CONFIG, StringDeserializer::class.java.name)
            put(ConsumerConfig.VALUE_DESERIALIZER_CLASS_CONFIG, StringDeserializer::class.java.name)
            put(ConsumerConfig.AUTO_OFFSET_RESET_CONFIG, "earliest")
            put(ConsumerConfig.ENABLE_AUTO_COMMIT_CONFIG, "true")

            // Defaults to plain unauthenticated Kafka (local/Docker). Set these three env vars
            // to point at a hosted broker instead (e.g. Upstash Kafka's free tier) without any
            // code change - useful when running natively without installing Kafka.
            val securityProtocol = System.getenv("KAFKA_SECURITY_PROTOCOL") ?: "PLAINTEXT"
            put("security.protocol", securityProtocol)
            if (securityProtocol != "PLAINTEXT") {
                put("sasl.mechanism", System.getenv("KAFKA_SASL_MECHANISM") ?: "PLAIN")
                put("sasl.jaas.config", System.getenv("KAFKA_SASL_JAAS_CONFIG") ?: "")
            }
        }

        val consumer = KafkaConsumer<String, String>(props)
        consumer.use {
            it.subscribe(listOf(topic))
            log.info("notification-service subscribed to {} as group {}", topic, consumerGroup)

            while (isActive) {
                val records = it.poll(Duration.ofMillis(500))
                for (record in records) {
                    try {
                        val event = mapper.readValue(record.value(), WebhookEventMessage::class.java)
                        retryManager.handle(event)
                    } catch (ex: Exception) {
                        log.warn("Skipping unparseable message at offset {}: {}", record.offset(), ex.message)
                    }
                }
            }
        }
    }
}
