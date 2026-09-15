from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, Response, status
from sqlmodel import Session

from app.database import get_session
from app.models import TransactionCreate, TransactionRead
from app.services.retail_analytics import RetailAnalyticsService

router = APIRouter(prefix="/api/transactions", tags=["transactions"])
DatabaseSession = Annotated[Session, Depends(get_session)]


@router.get("", response_model=list[TransactionRead])
def list_transactions(session: DatabaseSession):
    return RetailAnalyticsService(session).list_transactions()


@router.get("/{transaction_id}", response_model=TransactionRead)
def get_transaction(transaction_id: int, session: DatabaseSession):
    transaction = RetailAnalyticsService(session).get_transaction(transaction_id)
    if transaction is None:
        raise HTTPException(status_code=404, detail="Transaction not found")
    return transaction


@router.post("", response_model=TransactionRead, status_code=status.HTTP_201_CREATED)
def add_transaction(payload: TransactionCreate, session: DatabaseSession):
    return RetailAnalyticsService(session).add_transaction(payload)


@router.delete("/{transaction_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_transaction(transaction_id: int, session: DatabaseSession):
    if not RetailAnalyticsService(session).delete_transaction(transaction_id):
        raise HTTPException(status_code=404, detail="Transaction not found")
    return Response(status_code=status.HTTP_204_NO_CONTENT)
