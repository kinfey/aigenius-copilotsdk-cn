from datetime import date

from app.models import TransactionCreate
from app.services.retail_analytics import RetailAnalyticsService


def test_lists_ten_transactions(session):
    assert len(RetailAnalyticsService(session).list_transactions()) == 10


def test_gets_transaction(session):
    assert RetailAnalyticsService(session).get_transaction(1).customer_id == "C001"


def test_missing_transaction_returns_none(session):
    assert RetailAnalyticsService(session).get_transaction(999) is None


def test_adds_transaction(session):
    created = RetailAnalyticsService(session).add_transaction(
        TransactionCreate(
            customerId="C006",
            amount=42,
            productCategory="Grocery",
            store="Sydney",
            transactionDate=date(2026, 4, 1),
        )
    )
    assert created.id is not None
    assert created.customer_id == "C006"


def test_deletes_transaction(session):
    assert RetailAnalyticsService(session).delete_transaction(1) is True
    assert RetailAnalyticsService(session).get_transaction(1) is None


def test_delete_missing_transaction(session):
    assert RetailAnalyticsService(session).delete_transaction(999) is False


def test_lists_four_segments(session):
    assert len(RetailAnalyticsService(session).list_segments()) == 4


def test_gets_segment(session):
    assert RetailAnalyticsService(session).get_segment(1).name == "High Value"


def test_missing_segment_returns_none(session):
    assert RetailAnalyticsService(session).get_segment(999) is None


def test_predicts_c003_as_high_value(session):
    prediction = RetailAnalyticsService(session).predict_segment("C003")
    assert prediction.predicted_segment == "High Value"
    assert prediction.confidence == 0.89


def test_prediction_has_expected_c003_features(session):
    prediction = RetailAnalyticsService(session).predict_segment("C003")
    assert prediction.top_features == [
        "high_total_spend",
        "multi_category",
        "total_1700",
    ]


def test_predicts_c002_as_at_risk(session):
    assert RetailAnalyticsService(session).predict_segment("C002").predicted_segment == "At Risk"


def test_predicts_unknown_customer_as_new(session):
    assert RetailAnalyticsService(session).predict_segment("C999").predicted_segment == "New"


def test_transactions_with_segments_keeps_all_rows(session):
    rows = RetailAnalyticsService(session).get_transactions_with_segments()
    assert len(rows) == 10
    assert all(segment for _, segment in rows)

