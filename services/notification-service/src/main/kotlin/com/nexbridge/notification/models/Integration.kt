package com.nexbridge.notification.models

import org.bson.types.ObjectId

/**
 * Read-only projection of backend/Models/Integration.cs - we only read the fields this
 * service actually needs. Never write to the "integrations" collection from here; the
 * .NET backend owns that data.
 */
data class IntegrationDoc(
    val _id: ObjectId? = null,
    val partnerId: String = "",
    val name: String = "",
    val type: String = "",
    val status: String = "",
    val endpointUrl: String = "",
    val apiKeyReference: String = ""
)
