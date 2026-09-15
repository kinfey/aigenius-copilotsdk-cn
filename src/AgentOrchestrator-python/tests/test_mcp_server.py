import sqlite3

import pytest

from mcp_server.server import (
    connect,
    get_customer_summary,
    get_transaction,
    list_segments,
    list_transactions,
    predict_segment,
)


def test_database_connection_is_read_only():
    with connect() as connection, pytest.raises(sqlite3.OperationalError):
        connection.execute('DELETE FROM "transaction"')


def test_mcp_lists_ten_transactions():
    assert len(list_transactions()) == 10


def test_mcp_filters_transactions_by_customer():
    assert len(list_transactions("C003")) == 2


def test_mcp_gets_transaction():
    assert get_transaction(1)["customer_id"] == "C001"


def test_mcp_missing_transaction():
    assert get_transaction(999) is None


def test_mcp_lists_four_segments():
    assert len(list_segments()) == 4


def test_mcp_customer_summary_total():
    assert get_customer_summary("C003")["totalSpend"] == 1700


def test_mcp_customer_summary_categories():
    assert get_customer_summary("C003")["categories"] == ["Electronics", "Fashion"]


def test_mcp_unknown_customer_summary():
    summary = get_customer_summary("C999")
    assert summary["transactionCount"] == 0
    assert summary["totalSpend"] == 0


def test_mcp_predicts_high_value():
    assert predict_segment("C003")["predictedSegment"] == "High Value"


def test_mcp_predicts_at_risk():
    assert predict_segment("C002")["predictedSegment"] == "At Risk"


def test_mcp_predicts_new():
    assert predict_segment("C999")["predictedSegment"] == "New"
