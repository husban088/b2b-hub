"""Pydantic models.

WebhookEventMessage mirrors backend/Messaging/WebhookEventMessage.cs field-for-field -
this is the same public Kafka contract the Java and Kotlin services also consume.
"""
from datetime import datetime
from typing import Optional
from pydantic import BaseModel, Field


class WebhookEventMessage(BaseModel):
    event_id: str = Field(alias="eventId", default="")
    integration_id: str = Field(alias="integrationId", default="")
    direction: str = ""              # "inbound" | "outbound"
    event_type: str = Field(alias="eventType", default="")
    status_code: int = Field(alias="statusCode", default=0)
    success: bool = False
    payload: Optional[str] = None
    error_message: Optional[str] = Field(alias="errorMessage", default=None)
    received_at: Optional[datetime] = Field(alias="receivedAt", default=None)

    class Config:
        populate_by_name = True


class IntegrationStats(BaseModel):
    integration_id: str
    total_events: int
    successful_events: int
    failed_events: int
    success_rate: float
    last_event_at: Optional[datetime] = None


class OverviewStats(BaseModel):
    total_integrations_tracked: int
    total_events: int
    successful_events: int
    failed_events: int
    success_rate: float


class DailyPoint(BaseModel):
    date: str
    total_events: int
    successful_events: int
    failed_events: int
