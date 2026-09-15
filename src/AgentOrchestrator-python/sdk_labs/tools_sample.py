from __future__ import annotations

from typing import Annotated

from copilot import CopilotClient, define_tool
from copilot.session import PermissionHandler
from copilot.session_events import SessionEventType
from copilot.tools import ToolInvocation
from pydantic import BaseModel, Field

from sdk_labs import model_picker
from sdk_labs._common import IdleWaiter

TRANSACTIONS = [
    ("C001", 850.0, "Electronics"),
    ("C001", 325.0, "Home"),
    ("C002", 120.0, "Fashion"),
    ("C002", 80.0, "Grocery"),
    ("C003", 1200.0, "Electronics"),
    ("C003", 500.0, "Fashion"),
]


class GetCustomerTotalParams(BaseModel):
    customer_id: Annotated[
        str, Field(description="Customer identifier, for example C003")
    ]


@define_tool(description="Gets the total amount a given retail customer has spent.")
def get_customer_total(
    params: GetCustomerTotalParams, _invocation: ToolInvocation
) -> str:
    matches = [row for row in TRANSACTIONS if row[0].casefold() == params.customer_id.casefold()]
    if not matches:
        return f"No transactions found for {params.customer_id}."
    total = sum(row[1] for row in matches)
    print(f"  [tool] get_customer_total({params.customer_id}) -> ${total:,.2f}")
    return (
        f"{params.customer_id} has {len(matches)} transactions "
        f"totalling ${total:,.2f}."
    )


@define_tool(description="Lists the product categories purchased by a retail customer.")
def get_customer_categories(
    params: GetCustomerTotalParams, _invocation: ToolInvocation
) -> str:
    categories = sorted(
        {
            row[2]
            for row in TRANSACTIONS
            if row[0].casefold() == params.customer_id.casefold()
        }
    )
    if not categories:
        return f"No transactions found for {params.customer_id}."
    print(f"  [tool] get_customer_categories({params.customer_id}) -> {', '.join(categories)}")
    return f"{params.customer_id} purchased from: {', '.join(categories)}."


async def run(requested_model_id: str | None = None) -> int:
    print("== Lab 03: tools ==\n")
    prompt = "Which categories has customer C003 bought from, and how much have they spent?"
    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1
        print(f"Model: {model_id}")
        print(f"Prompt: {prompt}\n")
        waiter = IdleWaiter()
        session = await client.create_session(
            model=model_id,
            streaming=False,
            tools=[get_customer_total, get_customer_categories],
            on_permission_request=PermissionHandler.approve_all,
        )

        def on_event(evt) -> None:
            if evt.type is SessionEventType.ASSISTANT_MESSAGE and evt.data.content:
                print(f"\nAssistant: {evt.data.content}")
            waiter.handle(evt)

        session.on(on_event)
        async with session:
            await session.send(prompt)
            await waiter.wait()
    return 0

