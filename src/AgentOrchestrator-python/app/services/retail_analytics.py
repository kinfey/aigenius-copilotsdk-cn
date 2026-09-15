from sqlmodel import Session, select

from app.models import Segment, SegmentPrediction, Transaction, TransactionCreate


class RetailAnalyticsService:
    def __init__(self, session: Session):
        self.session = session

    def list_transactions(self) -> list[Transaction]:
        return list(self.session.exec(select(Transaction).order_by(Transaction.id)).all())

    def get_transactions_with_segments(self) -> list[tuple[Transaction, str]]:
        rows: list[tuple[Transaction, str]] = []
        for transaction in self.list_transactions():
            prediction = self.predict_segment(transaction.customer_id)
            rows.append((transaction, prediction.predicted_segment))
        return rows

    def get_transaction(self, transaction_id: int) -> Transaction:
        transaction = self.session.get(Transaction, transaction_id)
        return transaction  # type: ignore[return-value]

    def add_transaction(self, payload: TransactionCreate) -> Transaction:
        transaction = Transaction(**payload.model_dump())
        self.session.add(transaction)
        self.session.commit()
        self.session.refresh(transaction)
        return transaction

    def delete_transaction(self, transaction_id: int) -> bool:
        transaction = self.session.get(Transaction, transaction_id)
        if transaction is None:
            return False
        self.session.delete(transaction)
        self.session.commit()
        return True

    def list_segments(self) -> list[Segment]:
        return list(self.session.exec(select(Segment).order_by(Segment.id)).all())

    def get_segment(self, segment_id: int) -> Segment | None:
        return self.session.get(Segment, segment_id)

    def predict_segment(self, customer_id: str) -> SegmentPrediction:
        transactions = list(
            self.session.exec(
                select(Transaction).where(Transaction.customer_id == customer_id)
            ).all()
        )
        total = sum(transaction.amount for transaction in transactions)
        categories = {transaction.product_category for transaction in transactions}
        if total >= 1000:
            return SegmentPrediction(
                customer_id=customer_id,
                predicted_segment="High Value",
                confidence=0.89,
                top_features=["high_total_spend", "multi_category", f"total_{int(total)}"],
            )
        if total >= 300:
            segment, confidence = "Regular", 0.78
        elif transactions:
            segment, confidence = "At Risk", 0.68
        else:
            segment, confidence = "New", 0.55
        return SegmentPrediction(
            customer_id=customer_id,
            predicted_segment=segment,
            confidence=confidence,
            top_features=[f"total_{int(total)}", f"categories_{len(categories)}"],
        )

