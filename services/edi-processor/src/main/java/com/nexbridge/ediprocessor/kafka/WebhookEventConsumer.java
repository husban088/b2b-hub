package com.nexbridge.ediprocessor.kafka;

import com.nexbridge.ediprocessor.service.EdiProcessingService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.stereotype.Component;

@Component
public class WebhookEventConsumer {

    private static final Logger log = LoggerFactory.getLogger(WebhookEventConsumer.class);
    private final EdiProcessingService ediProcessingService;

    public WebhookEventConsumer(EdiProcessingService ediProcessingService) {
        this.ediProcessingService = ediProcessingService;
    }

    @KafkaListener(topics = "${nexbridge.kafka.topic:webhook.events.v1}",
                   groupId = "${nexbridge.kafka.consumer-group:edi-processor}",
                   containerFactory = "kafkaListenerContainerFactory")
    public void onWebhookEvent(WebhookEventMessage event) {
        try {
            boolean handled = ediProcessingService.processIfEdi(event);
            if (handled) {
                log.info("Processed EDI event {} for integration {}", event.getEventId(), event.getIntegrationId());
            } else {
                log.debug("Ignored non-EDI event {} for integration {}", event.getEventId(), event.getIntegrationId());
            }
        } catch (Exception e) {
            // Never let a bad/unexpected payload kill the consumer loop - log it and let the
            // error handler's backoff/retry policy (see KafkaConsumerConfig) take over.
            log.error("Error processing event {}: {}", event.getEventId(), e.getMessage(), e);
            throw e;
        }
    }
}
