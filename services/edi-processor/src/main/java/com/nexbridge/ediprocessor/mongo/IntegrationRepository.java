package com.nexbridge.ediprocessor.mongo;

import org.springframework.data.mongodb.repository.MongoRepository;
import java.util.Optional;

public interface IntegrationRepository extends MongoRepository<IntegrationDocument, String> {
    Optional<IntegrationDocument> findById(String id);
}
