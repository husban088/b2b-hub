import org.jetbrains.kotlin.gradle.tasks.KotlinCompile

plugins {
    kotlin("jvm") version "1.9.24"
    application
}

group = "com.nexbridge"
version = "1.0.0"

repositories {
    mavenCentral()
}

val kotlinCoroutinesVersion = "1.8.1"
val kafkaClientsVersion = "3.7.0"
val mongoDriverVersion = "5.1.1"
val ktorVersion = "2.3.12"
val logbackVersion = "1.5.6"

dependencies {
    // Coroutines - the whole service (consumer loop + retry scheduling) is coroutine-based.
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core:$kotlinCoroutinesVersion")

    // Kafka consumer (plain Java client - no Spring needed for a service this small).
    implementation("org.apache.kafka:kafka-clients:$kafkaClientsVersion")

    // Mongo (failure-count + alert bookkeeping, and reading Integration docs written by the .NET backend).
    implementation("org.mongodb:mongodb-driver-kotlin-coroutine:$mongoDriverVersion")
    implementation("org.mongodb:bson-kotlinx:$mongoDriverVersion")

    // Ktor client - used to re-POST failed outbound webhooks to the partner endpoint.
    implementation("io.ktor:ktor-client-core:$ktorVersion")
    implementation("io.ktor:ktor-client-cio:$ktorVersion")
    implementation("io.ktor:ktor-client-content-negotiation:$ktorVersion")

    // Ktor server - just exposes GET /health for the Docker healthcheck, same shape as the
    // Java service's actuator health endpoint.
    implementation("io.ktor:ktor-server-core:$ktorVersion")
    implementation("io.ktor:ktor-server-netty:$ktorVersion")

    // JSON (de)serialization of the webhook.events.v1 Kafka message contract.
    implementation("com.fasterxml.jackson.module:jackson-module-kotlin:2.17.1")
    implementation("com.fasterxml.jackson.datatype:jackson-datatype-jsr310:2.17.1")

    implementation("ch.qos.logback:logback-classic:$logbackVersion")

    testImplementation(kotlin("test"))
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:$kotlinCoroutinesVersion")
}

application {
    mainClass.set("com.nexbridge.notification.ApplicationKt")
}

tasks.withType<KotlinCompile> {
    kotlinOptions.jvmTarget = "17"
}

tasks.test {
    useJUnitPlatform()
}

tasks.jar {
    manifest {
        attributes["Main-Class"] = "com.nexbridge.notification.ApplicationKt"
    }
    duplicatesStrategy = DuplicatesStrategy.EXCLUDE
    from(configurations.runtimeClasspath.get().map { if (it.isDirectory) it else zipTree(it) })
}
