package com.nexbridge.ediprocessor.mongo;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;
import java.util.List;

/**
 * New collection owned entirely by this service - Spring Data's default (camelCase,
 * matches the Java field names) naming is fine here since nothing else reads or writes it.
 */
@Document(collection = "edi_processed_events")
public class EdiProcessedEvent {

    @Id
    private String id;

    @Indexed(unique = true)
    private String eventId;

    private String integrationId;
    private String eventType;
    private String direction;

    private boolean parseSuccess;
    private String parseErrorMessage;

    private String interchangeControlNumber;
    private String senderId;
    private String receiverId;
    private String functionalGroupId;
    private String transactionSetId;
    private String transactionSetControlNumber;
    private int segmentCount;
    private List<String> segments;

    private String rawPayload;
    private Instant processedAt = Instant.now();

    public String getId() { return id; }
    public void setId(String id) { this.id = id; }
    public String getEventId() { return eventId; }
    public void setEventId(String eventId) { this.eventId = eventId; }
    public String getIntegrationId() { return integrationId; }
    public void setIntegrationId(String integrationId) { this.integrationId = integrationId; }
    public String getEventType() { return eventType; }
    public void setEventType(String eventType) { this.eventType = eventType; }
    public String getDirection() { return direction; }
    public void setDirection(String direction) { this.direction = direction; }
    public boolean isParseSuccess() { return parseSuccess; }
    public void setParseSuccess(boolean parseSuccess) { this.parseSuccess = parseSuccess; }
    public String getParseErrorMessage() { return parseErrorMessage; }
    public void setParseErrorMessage(String parseErrorMessage) { this.parseErrorMessage = parseErrorMessage; }
    public String getInterchangeControlNumber() { return interchangeControlNumber; }
    public void setInterchangeControlNumber(String interchangeControlNumber) { this.interchangeControlNumber = interchangeControlNumber; }
    public String getSenderId() { return senderId; }
    public void setSenderId(String senderId) { this.senderId = senderId; }
    public String getReceiverId() { return receiverId; }
    public void setReceiverId(String receiverId) { this.receiverId = receiverId; }
    public String getFunctionalGroupId() { return functionalGroupId; }
    public void setFunctionalGroupId(String functionalGroupId) { this.functionalGroupId = functionalGroupId; }
    public String getTransactionSetId() { return transactionSetId; }
    public void setTransactionSetId(String transactionSetId) { this.transactionSetId = transactionSetId; }
    public String getTransactionSetControlNumber() { return transactionSetControlNumber; }
    public void setTransactionSetControlNumber(String transactionSetControlNumber) { this.transactionSetControlNumber = transactionSetControlNumber; }
    public int getSegmentCount() { return segmentCount; }
    public void setSegmentCount(int segmentCount) { this.segmentCount = segmentCount; }
    public List<String> getSegments() { return segments; }
    public void setSegments(List<String> segments) { this.segments = segments; }
    public String getRawPayload() { return rawPayload; }
    public void setRawPayload(String rawPayload) { this.rawPayload = rawPayload; }
    public Instant getProcessedAt() { return processedAt; }
    public void setProcessedAt(Instant processedAt) { this.processedAt = processedAt; }
}
