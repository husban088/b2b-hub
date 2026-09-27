package com.nexbridge.ediprocessor.service;

import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.List;

@Service
public class EdiParserService {

    private static final String ELEMENT_SEPARATOR = "\\*";

    public EdiParseResult parse(String rawPayload) {
        if (rawPayload == null || rawPayload.isBlank()) {
            return EdiParseResult.failure("Payload is empty - nothing to parse.");
        }

        List<String> segments = splitIntoSegments(rawPayload);
        if (segments.isEmpty()) {
            return EdiParseResult.failure("No EDI segments found in payload.");
        }

        EdiParseResult result = new EdiParseResult();
        result.success = true;
        result.segments = segments;
        result.segmentCount = segments.size();

        for (String segment : segments) {
            String[] elements = segment.split(ELEMENT_SEPARATOR);
            if (elements.length == 0) {
                continue;
            }
            switch (elements[0].trim().toUpperCase()) {
                case "ISA" -> {
                    result.senderId = elementAt(elements, 6);
                    result.receiverId = elementAt(elements, 8);
                    result.interchangeControlNumber = elementAt(elements, 13);
                }
                case "GS" -> result.functionalGroupId = elementAt(elements, 1);
                case "ST" -> {
                    result.transactionSetId = elementAt(elements, 1);
                    result.transactionSetControlNumber = elementAt(elements, 2);
                }
                default -> { }
            }
        }

        if (result.interchangeControlNumber == null && result.transactionSetId == null) {
            return EdiParseResult.failure(
                    "Payload did not contain a recognizable ISA or ST segment; not valid X12 EDI.");
        }

        return result;
    }

    private List<String> splitIntoSegments(String rawPayload) {
        String trimmed = rawPayload.trim();
        String[] rawSegments = trimmed.contains("~")
                ? trimmed.split("~")
                : trimmed.split("\\r?\\n");

        List<String> segments = new ArrayList<>();
        for (String s : rawSegments) {
            String cleaned = s.trim();
            if (!cleaned.isEmpty()) {
                segments.add(cleaned);
            }
        }
        return segments;
    }

    private String elementAt(String[] elements, int index) {
        return index < elements.length ? elements[index].trim() : null;
    }

    public static class EdiParseResult {
        private boolean success;
        private String errorMessage;
        private String interchangeControlNumber;
        private String senderId;
        private String receiverId;
        private String functionalGroupId;
        private String transactionSetId;
        private String transactionSetControlNumber;
        private int segmentCount;
        private List<String> segments = List.of();

        static EdiParseResult failure(String message) {
            EdiParseResult r = new EdiParseResult();
            r.success = false;
            r.errorMessage = message;
            return r;
        }

        public boolean isSuccess() { return success; }
        public String getErrorMessage() { return errorMessage; }
        public String getInterchangeControlNumber() { return interchangeControlNumber; }
        public String getSenderId() { return senderId; }
        public String getReceiverId() { return receiverId; }
        public String getFunctionalGroupId() { return functionalGroupId; }
        public String getTransactionSetId() { return transactionSetId; }
        public String getTransactionSetControlNumber() { return transactionSetControlNumber; }
        public int getSegmentCount() { return segmentCount; }
        public List<String> getSegments() { return segments; }
    }
}
