package com.nexbridge.notification

import com.nexbridge.notification.kafka.EventConsumer
import com.nexbridge.notification.mongo.MongoClientProvider
import io.ktor.http.*
import io.ktor.server.application.*
import io.ktor.server.engine.*
import io.ktor.server.netty.*
import io.ktor.server.response.*
import io.ktor.server.routing.*
import kotlinx.coroutines.*
import org.slf4j.LoggerFactory

private val log = LoggerFactory.getLogger("notification-service")

fun main() {
    val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    // Ktor server just for /health, mirroring the Java EDI processor's actuator/health -
    // Docker Compose healthcheck and any load balancer probe hits this.
    val port = (System.getenv("NOTIFICATION_SERVICE_PORT") ?: "8082").toInt()
    val server = embeddedServer(Netty, port = port) {
        routing {
            get("/health") {
                call.respondText("""{"status":"UP"}""", ContentType.Application.Json)
            }
        }
    }
    server.start(wait = false)
    log.info("notification-service health endpoint listening on :{}", port)

    val consumerJob = serviceScope.launch {
        try {
            EventConsumer(serviceScope).run()
        } catch (ex: Exception) {
            log.error("Kafka consumer loop crashed - the service will keep the health endpoint up but is not consuming.", ex)
        }
    }

    Runtime.getRuntime().addShutdownHook(Thread {
        log.info("Shutting down notification-service...")
        consumerJob.cancel()
        MongoClientProvider.close()
        server.stop(1000, 2000)
    })

    // Keep the main thread alive - embeddedServer(wait = false) returns immediately.
    runBlocking { consumerJob.join() }
}
