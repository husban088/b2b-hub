package com.nexbridge.ediprocessor.mongo;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;
import org.springframework.data.mongodb.core.mapping.Field;

/**
 * Read-only view of the "integrations" collection, which is owned and written by the
 * .NET backend (backend/Models/Integration.cs) - this service never writes to it.
 *
 * IMPORTANT: the .NET side does not register a camelCase Mongo convention, so BSON field
 * names match the C# property names exactly, PascalCase included (e.g. "Type", not "type").
 * Spring Data's default naming would otherwise look for a lowercase "type" field and always
 * get null - the explicit @Field("Type") below is required, not cosmetic.
 */
@Document(collection = "integrations")
public class IntegrationDocument {

    @Id
    private String id;

    @Field("Type")
    private Object type;

    public String getId() { return id; }
    public void setId(String id) { this.id = id; }
    public Object getType() { return type; }
    public void setType(Object type) { this.type = type; }
}
