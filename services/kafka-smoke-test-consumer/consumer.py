"""
Quick standalone script to prove the Kafka pipeline works end-to-end.
This is NOT the real analytics service - it's just a verification tool.

Run:
    pip install -r requirements.txt
    python consumer.py

Then, in another terminal, trigger a webhook event against the backend:
    curl -X POST http://localhost:8080/api/webhooks/<an-existing-integration-id> \
         -H "Content-Type: application/json" \
         -d '{"eventType": "order.created", "payload": {"orderId": 123}}'

You should see the event printed here within a second or two. If nothing
shows up, check:
  - docker compose ps        (kafka container should be "healthy")
  - http://localhost:8090    (Kafka UI - confirm "webhook.events.v1" has messages)
"""
import json
import os

from confluent_kafka import Consumer

BOOTSTRAP_SERVERS = os.environ.get("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")
TOPIC = "webhook.events.v1"


def main() -> None:
    consumer = Consumer({
        "bootstrap.servers": BOOTSTRAP_SERVERS,
        "group.id": "kafka-smoke-test",
        "auto.offset.reset": "earliest",
    })
    consumer.subscribe([TOPIC])
    print(f"Listening on '{TOPIC}' at {BOOTSTRAP_SERVERS} ... (Ctrl+C to stop)")

    try:
        while True:
            msg = consumer.poll(1.0)
            if msg is None:
                continue
            if msg.error():
                print(f"Consumer error: {msg.error()}")
                continue

            event = json.loads(msg.value().decode("utf-8"))
            print(
                f"[{event.get('receivedAt')}] {event.get('direction')} "
                f"{event.get('eventType')} -> integration {event.get('integrationId')} "
                f"(success={event.get('success')})"
            )
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        consumer.close()


if __name__ == "__main__":
    main()
