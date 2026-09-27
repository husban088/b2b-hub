package com.nexbridge.notification.mongo

import com.mongodb.kotlin.client.coroutine.MongoClient
import com.mongodb.kotlin.client.coroutine.MongoDatabase

/**
 * Single shared Mongo connection for the service.
 *
 * Collections this service reads:
 *   - "integrations" (owned by the .NET backend - read-only, for endpointUrl lookups)
 *
 * Collections this service owns (safe to read/write, nobody else touches these):
 *   - "integration_failure_state" - one doc per integrationId: consecutive failure count
 *     and whether an alert has already fired for the current streak.
 *   - "integration_alerts"        - one doc per alert fired, for the dashboard to show
 *     ("Partner X's webhook has failed 3 times in a row").
 */
object MongoClientProvider {
    private val uri = System.getenv("MONGODB_URI") ?: "mongodb://localhost:27017/b2b_integration_hub"
    private val dbName = System.getenv("MONGODB_DATABASE") ?: "b2b_integration_hub"

    val client: MongoClient by lazy { MongoClient.create(uri) }
    val database: MongoDatabase by lazy { client.getDatabase(dbName) }

    fun close() = client.close()
}
