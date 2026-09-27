"""Turns one WebhookEventMessage into two Mongo upserts:
  1. the integration's running totals (integration_analytics)
  2. today's rollup for that integration (integration_analytics_daily)

Kept deliberately simple (counts + success rate) - it aggregates whatever
fields the shared Kafka contract actually has today (no per-event latency
field exists yet, so latency isn't tracked here; add it once the producer
side starts publishing one).
"""
import logging
from datetime import datetime, timezone

from app.db import get_db
from app.models import WebhookEventMessage

log = logging.getLogger("analytics-service")


async def record_event(event: WebhookEventMessage) -> None:
    db = get_db()
    received_at = event.received_at or datetime.now(timezone.utc)
    day_key = received_at.strftime("%Y-%m-%d")

    inc_fields = {
        "totalEvents": 1,
        "successfulEvents": 1 if event.success else 0,
        "failedEvents": 0 if event.success else 1,
    }

    try:
        await db.integration_analytics.update_one(
            {"integrationId": event.integration_id},
            {
                "$inc": inc_fields,
                "$set": {"lastEventAt": received_at},
                "$setOnInsert": {"integrationId": event.integration_id},
            },
            upsert=True,
        )

        await db.integration_analytics_daily.update_one(
            {"integrationId": event.integration_id, "date": day_key},
            {
                "$inc": inc_fields,
                "$setOnInsert": {"integrationId": event.integration_id, "date": day_key},
            },
            upsert=True,
        )
    except Exception:
        # Mongo hiccup shouldn't crash the consumer loop - the next event will retry the same shape of write.
        log.exception("Failed to record analytics for event %s (integration %s)", event.event_id, event.integration_id)
