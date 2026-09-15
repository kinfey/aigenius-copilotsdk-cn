from datetime import date

import pytest
from sqlmodel import Session, SQLModel, create_engine
from sqlmodel.pool import StaticPool

from app.database import SEGMENTS, TRANSACTIONS, init_db
from app.models import Segment, Transaction


@pytest.fixture(scope="session", autouse=True)
def initialize_application_database():
    init_db()


@pytest.fixture
def session():
    engine = create_engine(
        "sqlite://",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    SQLModel.metadata.create_all(engine)
    with Session(engine) as database:
        database.add_all(
            Transaction(
                customer_id=customer,
                amount=amount,
                product_category=category,
                store=store,
                transaction_date=date.fromisoformat(transaction_date),
            )
            for customer, amount, category, store, transaction_date in TRANSACTIONS
        )
        database.add_all(
            Segment(
                name=name,
                customer_count=count,
                average_spend=spend,
                retention_rate=retention,
            )
            for name, count, spend, retention in SEGMENTS
        )
        database.commit()
        yield database

