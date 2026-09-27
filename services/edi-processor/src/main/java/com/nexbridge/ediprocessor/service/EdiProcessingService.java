package com.nexbridge.ediprocessor.service;

import com.nexbridge.ediprocessor.kafka.WebhookEventMessage;
import com.nexbridge.ediprocessor.mongo.EdiProcessedEvent;
import com.nexbridge.ediprocessor.mongo.EdiProcessedEventRepository;
import com.nexbridge.ediprocessor.mongo.IntegrationDocument;
import com.nexbridge.ediprocessor.mongo.IntegrationRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DuplicateKeyException;
import org.springframework.stereotype.Service;

import java.util.Optional;

@Service
public class EdiProcessingService {

    private static final Logger log = LoggerFactory.getLogger(EdiProcessingService.class);
    // backend/Models/Integration.cs: enum IntegrationType { RestApi, Graphql, Webhook, FileSync, Edi }
    // Covers both possible Mongo representations: the MongoDB C# driver's default enum
    // serializer can store either the ordinal (int) or the name (string) depending on
    // driver version/config, so we accept either "4" or "Edi" as a match.
    private static final int EDI_TYPE_ORDINAL = 4;
    private static final String EDI_TYPE_NAME = "EDI";

    private final IntegrationRepository integrationRepository;
    private final EdiProcessedEventRepository processedEventRepository;
    private final EdiParserService ediParserService;

    public EdiProcessingService(IntegrationRepository integrationRepository,
                                 EdiProcessedEventRepository processedEventRepository,
                                 EdiParserService ediParserService) {
        this.integrationRepository = integrationRepository;
        this.processedEventRepository = processedEventRepository;
        this.ediParserService = ediParserService;
    }

    /**
     * @return true if the event belonged to an EDI integration and was handled
     *         (parsed and stored, or already stored from a previous delivery);
     *         false if it was skipped because it isn't an EDI event.
     */
    public boolean processIfEdi(WebhookEventMessage event) {
        if (event.getEventId() == null || event.getIntegrationId() == null) {
            log.warn("Skipping event with missing eventId/integrationId: {}", event);
            return false;
        }

        if (processedEventRepository.findByEventId(event.getEventId()).isPresent()) {
            log.debug("Event {} already processed, skipping.", event.getEventId());
            return true;
        }

        Optional<IntegrationDocument> integration = integrationRepository.findById(event.getIntegrationId());
        if (integration.isEmpty()) {
            log.warn("Integration {} referenced by event {} not found - skipping.",
                    event.getIntegrationId(), event.getEventId());
            return false;
        }

        if (!isEdiType(integration.get().getType())) {
            return false;
        }

        EdiProcessedEvent doc = new EdiProcessedEvent();
        doc.setEventId(event.getEventId());
        doc.setIntegrationId(event.getIntegrationId());
        doc.setEventType(event.getEventType());
        doc.setDirection(event.getDirection());
        doc.setRawPayload(event.getPayload());

        EdiParserService.EdiParseResult parsed = ediParserService.parse(event.getPayload());
        doc.setParseSuccess(parsed.isSuccess());
        if (parsed.isSuccess()) {
            doc.setInterchangeControlNumber(parsed.getInterchangeControlNumber());
            doc.setSenderId(parsed.getSenderId());
            doc.setReceiverId(parsed.getReceiverId());
            doc.setFunctionalGroupId(parsed.getFunctionalGroupId());
            doc.setTransactionSetId(parsed.getTransactionSetId());
            doc.setTransactionSetControlNumber(parsed.getTransactionSetControlNumber());
            doc.setSegmentCount(parsed.getSegmentCount());
            doc.setSegments(parsed.getSegments());
        } else {
            // Never throw on a bad payload - record the failure and move on so the
            // consumer loop keeps running for the next message.
            doc.setParseErrorMessage(parsed.getErrorMessage());
            log.warn("EDI parse failed for event {}: {}", event.getEventId(), parsed.getErrorMessage());
        }

        try {
            processedEventRepository.save(doc);
        } catch (DuplicateKeyException e) {
            log.debug("Event {} was already saved by a concurrent consumer.", event.getEventId());
        }

        return true;
    }

    private boolean isEdiType(Object type) {
        if (type == null) {
            return false;
        }
        if (type instanceof Number number) {
            return number.intValue() == EDI_TYPE_ORDINAL;
        }
        return EDI_TYPE_NAME.equalsIgnoreCase(String.valueOf(type))
                || "Edi".equalsIgnoreCase(String.valueOf(type));
    }
}
