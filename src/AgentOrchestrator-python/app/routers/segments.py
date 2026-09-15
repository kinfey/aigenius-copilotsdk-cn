from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException
from sqlmodel import Session

from app.database import get_session
from app.models import SegmentPrediction, SegmentRead
from app.services.retail_analytics import RetailAnalyticsService

router = APIRouter(prefix="/api/segments", tags=["segments"])
DatabaseSession = Annotated[Session, Depends(get_session)]


@router.get("", response_model=list[SegmentRead])
def list_segments(session: DatabaseSession):
    return RetailAnalyticsService(session).list_segments()


@router.get("/predict/{customer_id}", response_model=SegmentPrediction)
def predict_segment(customer_id: str, session: DatabaseSession):
    return RetailAnalyticsService(session).predict_segment(customer_id)


@router.get("/{segment_id}", response_model=SegmentRead)
def get_segment(segment_id: int, session: DatabaseSession):
    segment = RetailAnalyticsService(session).get_segment(segment_id)
    if segment is None:
        raise HTTPException(status_code=404, detail="Segment not found")
    return segment
