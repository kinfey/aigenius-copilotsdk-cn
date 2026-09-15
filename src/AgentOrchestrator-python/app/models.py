from __future__ import annotations

from datetime import date

from pydantic import BaseModel, ConfigDict
from sqlmodel import Field, SQLModel


def to_camel(value: str) -> str:
    parts = value.split("_")
    return parts[0] + "".join(part.title() for part in parts[1:])


class ApiModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class Transaction(SQLModel, table=True):
    id: int | None = Field(default=None, primary_key=True)
    customer_id: str
    amount: float
    product_category: str
    store: str
    transaction_date: date


class TransactionCreate(ApiModel):
    customer_id: str
    amount: float
    product_category: str
    store: str
    transaction_date: date


class TransactionRead(ApiModel):
    id: int
    customer_id: str
    amount: float
    product_category: str
    store: str
    transaction_date: date


class Segment(SQLModel, table=True):
    id: int | None = Field(default=None, primary_key=True)
    name: str
    customer_count: int
    average_spend: float
    retention_rate: float


class SegmentRead(ApiModel):
    id: int
    name: str
    customer_count: int
    average_spend: float
    retention_rate: float


class SegmentPrediction(ApiModel):
    customer_id: str
    predicted_segment: str
    confidence: float
    top_features: list[str]


class ChatRequest(ApiModel):
    prompt: str = ""
    model: str = "gpt-6-astra"
    system_message: str | None = None


class ModelRead(ApiModel):
    id: str
    name: str
    description: str = ""
