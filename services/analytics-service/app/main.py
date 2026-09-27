import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from app.consumer import start_background_consumer, stop_background_consumer
from app.db import get_db
from app.models import DailyPoint, IntegrationStats, OverviewStats

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s - %(message)s")
log = logging.getLogger("analytics-service")


@asynccontextmanager
async def lifespan(app: FastAPI):
    log.info("Starting background Kafka consumer thread...")
    start_background_consumer()
    yield
    log.info("Shutting down background Kafka consumer thread...")
    stop_background_consumer()


app = FastAPI(title="Nexbridge Analytics Service", version="1.0.0", lifespan=lifespan)

# Same CORS story as the .NET backend - the Angular dev server needs to call this directly.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:4200", "http://localhost"],
    allow_methods=["GET"],
    allow_headers=["*"],
)


@app.get("/health")
async def health():
    return {"status": "UP"}


@app.get("/analytics/overview", response_model=OverviewStats)
async def overview():
    db = get_db()
    cursor = db.integration_analytics.find({})
    total_events = successful = failed = tracked = 0
    async for doc in cursor:
        tracked += 1
        total_events += doc.get("totalEvents", 0)
        successful += doc.get("successfulEvents", 0)
        failed += doc.get("failedEvents", 0)

    success_rate = round((successful / total_events) * 100, 2) if total_events else 0.0
    return OverviewStats(
        total_integrations_tracked=tracked,
        total_events=total_events,
        successful_events=successful,
        failed_events=failed,
        success_rate=success_rate,
    )


@app.get("/analytics/integrations/{integration_id}", response_model=IntegrationStats)
async def integration_stats(integration_id: str):
    db = get_db()
    doc = await db.integration_analytics.find_one({"integrationId": integration_id})
    if not doc:
        raise HTTPException(status_code=404, detail="No analytics recorded yet for this integration.")

    total = doc.get("totalEvents", 0)
    successful = doc.get("successfulEvents", 0)
    return IntegrationStats(
        integration_id=integration_id,
        total_events=total,
        successful_events=successful,
        failed_events=doc.get("failedEvents", 0),
        success_rate=round((successful / total) * 100, 2) if total else 0.0,
        last_event_at=doc.get("lastEventAt"),
    )


@app.get("/analytics/timeseries", response_model=list[DailyPoint])
async def timeseries(days: int = 7, integration_id: str | None = None):
    if days < 1 or days > 90:
        raise HTTPException(status_code=400, detail="days must be between 1 and 90.")

    db = get_db()
    query: dict = {}
    if integration_id:
        query["integrationId"] = integration_id

    pipeline = [
        {"$match": query},
        {
            "$group": {
                "_id": "$date",
                "total_events": {"$sum": "$totalEvents"},
                "successful_events": {"$sum": "$successfulEvents"},
                "failed_events": {"$sum": "$failedEvents"},
            }
        },
        {"$sort": {"_id": -1}},
        {"$limit": days},
    ]

    results = [doc async for doc in db.integration_analytics_daily.aggregate(pipeline)]
    return [
        DailyPoint(
            date=doc["_id"],
            total_events=doc["total_events"],
            successful_events=doc["successful_events"],
            failed_events=doc["failed_events"],
        )
        for doc in sorted(results, key=lambda d: d["_id"])
    ]
