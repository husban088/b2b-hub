"""Background Kafka consumer for webhook.events.v1.

kafka-python is a blocking/synchronous library, so it runs on its own thread
with its own synchronous pymongo client - this deliberately does NOT touch
the async motor client the FastAPI endpoints use, to avoid cross-thread
event-loop issues. It's the same aggregation the async aggregator.py does,
kept in sync form for this thread.
"""
import json
import logging
import os
import threading
import time
from datetime import datetime, timezone

from kafka import KafkaConsumer
from kafka.errors import NoBrokersAvailable
from pymongo import MongoClient

log = logging.getLogger("analytics-service.consumer")

KAFKA_BOOTSTRAP_SERVERS = os.getenv("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")
KAFKA_TOPIC = os.getenv("ANALYTICS_KAFKA_TOPIC", "webhook.events.v1")
CONSUMER_GROUP = os.getenv("ANALYTICS_CONSUMER_GROUP", "analytics-service")
MONGODB_URI = os.getenv("MONGODB_URI", "mongodb://localhost:27017")
MONGODB_DATABASE = os.getenv("MONGODB_DATABASE", "b2b_integration_hub")

_stop_event = threading.Event()


def _record_event_sync(db, event: dict) -> None:
    integration_id = event.get("integrationId", "")
    success = bool(event.get("success", False))
    received_at_raw = event.get("receivedAt")
    try:
        received_at = datetime.fromisoformat(received_at_raw.replace("Z", "+00:00")) if received_at_raw else datetime.now(timezone.utc)
    except (ValueError, AttributeError):
        received_at = datetime.now(timezone.utc)
    day_key = received_at.strftime("%Y-%m-%d")

    inc_fields = {
        "totalEvents": 1,
        "successfulEvents": 1 if success else 0,
        "failedEvents": 0 if success else 1,
    }

    db.integration_analytics.update_one(
        {"integrationId": integration_id},
        {
            "$inc": inc_fields,
            "$set": {"lastEventAt": received_at},
            "$setOnInsert": {"integrationId": integration_id},
        },
        upsert=True,
    )
    db.integration_analytics_daily.update_one(
        {"integrationId": integration_id, "date": day_key},
        {
            "$inc": inc_fields,
            "$setOnInsert": {"integrationId": integration_id, "date": day_key},
        },
        upsert=True,
    )


def _consume_loop() -> None:
    mongo_client = MongoClient(MONGODB_URI)
    db = mongo_client[MONGODB_DATABASE]

    consumer = None
    while not _stop_event.is_set() and consumer is None:
        try:
            # Defaults to plain unauthenticated Kafka (local/Docker). Set these three env vars
            # to point at a hosted broker instead (e.g. Upstash Kafka's free tier) without any
            # code change - useful when running natively without installing Kafka.
            security_protocol = os.getenv("KAFKA_SECURITY_PROTOCOL", "PLAINTEXT")
            kafka_kwargs = dict(
                bootstrap_servers=KAFKA_BOOTSTRAP_SERVERS,
                group_id=CONSUMER_GROUP,
                auto_offset_reset="earliest",
                enable_auto_commit=True,
                value_deserializer=lambda v: json.loads(v.decode("utf-8")),
                consumer_timeout_ms=1000,
                security_protocol=security_protocol,
            )
            if security_protocol != "PLAINTEXT":
                kafka_kwargs["sasl_mechanism"] = os.getenv("KAFKA_SASL_MECHANISM", "SCRAM-SHA-256")
                kafka_kwargs["sasl_plain_username"] = os.getenv("KAFKA_SASL_USERNAME", "")
                kafka_kwargs["sasl_plain_password"] = os.getenv("KAFKA_SASL_PASSWORD", "")

            consumer = KafkaConsumer(KAFKA_TOPIC, **kafka_kwargs)
            log.info("analytics-service subscribed to %s as group %s", KAFKA_TOPIC, CONSUMER_GROUP)
        except NoBrokersAvailable:
            log.warning("Kafka not reachable yet at %s - retrying in 5s.", KAFKA_BOOTSTRAP_SERVERS)
            time.sleep(5)

    while not _stop_event.is_set():
        try:
            for message in consumer:
                if _stop_event.is_set():
                    break
                try:
                    _record_event_sync(db, message.value)
                except Exception:
                    log.exception("Failed to record analytics for offset %s", message.offset)
        except Exception:
            log.exception("Kafka poll loop error - retrying in 5s.")
            time.sleep(5)


def start_background_consumer() -> threading.Thread:
    thread = threading.Thread(target=_consume_loop, name="kafka-consumer", daemon=True)
    thread.start()
    return thread


def stop_background_consumer() -> None:
    _stop_event.set()
