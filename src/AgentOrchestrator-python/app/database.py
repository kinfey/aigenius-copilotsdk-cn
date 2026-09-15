from pathlib import Path

from sqlmodel import Session, SQLModel, create_engine, select

from app.models import Segment, Transaction

PROJECT_ROOT = Path(__file__).resolve().parents[1]
DATABASE_PATH = PROJECT_ROOT / "retail.db"
engine = create_engine(
    f"sqlite:///{DATABASE_PATH}",
    connect_args={"check_same_thread": False},
)


TRANSACTIONS = [
    ("C001", 850.0, "Electronics", "Sydney", "2026-01-05"),
    ("C001", 325.0, "Home", "Melbourne", "2026-01-18"),
    ("C002", 120.0, "Fashion", "Sydney", "2026-02-02"),
    ("C002", 80.0, "Grocery", "Brisbane", "2026-02-08"),
    ("C003", 1200.0, "Electronics", "Melbourne", "2026-02-14"),
    ("C003", 500.0, "Fashion", "Sydney", "2026-03-01"),
    ("C004", 95.0, "Grocery", "Perth", "2026-03-02"),
    ("C004", 60.0, "Home", "Perth", "2026-03-04"),
    ("C005", 220.0, "Fashion", "Brisbane", "2026-03-08"),
    ("C005", 140.0, "Electronics", "Sydney", "2026-03-10"),
]

SEGMENTS = [
    ("High Value", 150, 850.0, 0.92),
    ("Regular", 3200, 180.0, 0.78),
    ("At Risk", 890, 95.0, 0.45),
    ("New", 420, 120.0, 0.65),
]


def init_db() -> None:
    SQLModel.metadata.create_all(engine)
    with Session(engine) as session:
        if session.exec(select(Transaction)).first() is None:
            from datetime import date

            session.add_all(
                Transaction(
                    customer_id=customer,
                    amount=amount,
                    product_category=category,
                    store=store,
                    transaction_date=date.fromisoformat(transaction_date),
                )
                for customer, amount, category, store, transaction_date in TRANSACTIONS
            )
        if session.exec(select(Segment)).first() is None:
            session.add_all(
                Segment(
                    name=name,
                    customer_count=count,
                    average_spend=spend,
                    retention_rate=retention,
                )
                for name, count, spend, retention in SEGMENTS
            )
        session.commit()


def get_session():
    with Session(engine) as session:
        yield session

