import sqlite3
from pathlib import Path

from mcp.server.fastmcp import FastMCP

DATABASE_PATH = Path(__file__).resolve().parents[1] / "retail.db"
mcp = FastMCP("retail-analytics")


def connect() -> sqlite3.Connection:
    connection = sqlite3.connect(f"file:{DATABASE_PATH}?mode=ro", uri=True)
    connection.row_factory = sqlite3.Row
    return connection


@mcp.tool()
def list_transactions(customer_id: str | None = None) -> list[dict]:
    """List retail transactions, optionally filtered by customer ID."""
    query = 'SELECT * FROM "transaction"'
    parameters: tuple[str, ...] = ()
    if customer_id:
        query += " WHERE customer_id = ?"
        parameters = (customer_id,)
    query += " ORDER BY id"
    with connect() as connection:
        return [dict(row) for row in connection.execute(query, parameters).fetchall()]


@mcp.tool()
def get_transaction(transaction_id: int) -> dict | None:
    """Get one retail transaction by numeric ID."""
    with connect() as connection:
        row = connection.execute(
            'SELECT * FROM "transaction" WHERE id = ?', (transaction_id,)
        ).fetchone()
    return dict(row) if row else None


@mcp.tool()
def list_segments() -> list[dict]:
    """List all customer segment definitions."""
    with connect() as connection:
        return [dict(row) for row in connection.execute("SELECT * FROM segment ORDER BY id")]


@mcp.tool()
def get_customer_summary(customer_id: str) -> dict:
    """Summarize total spend, transaction count, and categories for a customer."""
    with connect() as connection:
        rows = connection.execute(
            'SELECT amount, product_category FROM "transaction" WHERE customer_id = ?',
            (customer_id,),
        ).fetchall()
    return {
        "customerId": customer_id,
        "transactionCount": len(rows),
        "totalSpend": sum(row["amount"] for row in rows),
        "categories": sorted({row["product_category"] for row in rows}),
    }


@mcp.tool()
def predict_segment(customer_id: str) -> dict:
    """Predict a customer's retail segment from their transaction history."""
    summary = get_customer_summary(customer_id)
    total = summary["totalSpend"]
    if total >= 1000:
        segment, confidence = "High Value", 0.89
    elif total >= 300:
        segment, confidence = "Regular", 0.78
    elif summary["transactionCount"]:
        segment, confidence = "At Risk", 0.68
    else:
        segment, confidence = "New", 0.55
    return {
        "customerId": customer_id,
        "predictedSegment": segment,
        "confidence": confidence,
    }


def main() -> None:
    mcp.run(transport="stdio")
