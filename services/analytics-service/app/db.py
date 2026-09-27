"""Shared Mongo (motor/async) client for the analytics service.

Collections this service owns (nobody else reads/writes these):
  - integration_analytics        one doc per integrationId: running totals
  - integration_analytics_daily  one doc per (integrationId, date): daily rollup for charts

Collection this service only reads:
  - integrations   (owned by the .NET backend, used to resolve partnerId / name for display)
"""
import os
from motor.motor_asyncio import AsyncIOMotorClient

MONGODB_URI = os.getenv("MONGODB_URI", "mongodb://localhost:27017")
MONGODB_DATABASE = os.getenv("MONGODB_DATABASE", "b2b_integration_hub")

_client: AsyncIOMotorClient | None = None


def get_client() -> AsyncIOMotorClient:
    global _client
    if _client is None:
        _client = AsyncIOMotorClient(MONGODB_URI)
    return _client


def get_db():
    return get_client()[MONGODB_DATABASE]
