package com.nexbridge.ediprocessor.mongo;

import org.springframework.data.mongodb.repository.MongoRepository;
import java.util.Optional;

public interface EdiProcessedEventRepository extends MongoRepository<EdiProcessedEvent, String> {
    Optional<EdiProcessedEvent> findByEventId(String eventId);
}
