from copilot import CopilotClient
from copilot.session_events import SessionEventType

from sdk_labs import model_picker
from sdk_labs._common import IdleWaiter, trim


async def run(requested_model_id: str | None = None) -> int:
    print("== Lab 04: events ==\n")
    prompt = "Name two retail KPIs. One line each."
    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1
        print(f"Model: {model_id}")
        print(f"Prompt: {prompt}\n")
        waiter = IdleWaiter()
        counters = {"order": 0, "deltas": 0}
        session = await client.create_session(model=model_id, streaming=True)

        def on_event(evt) -> None:
            if evt.type is SessionEventType.ASSISTANT_MESSAGE_DELTA:
                counters["deltas"] += 1
                return
            counters["order"] += 1
            print(f"{counters['order']:3d}. {evt.type.value}")
            if evt.type is SessionEventType.ASSISTANT_MESSAGE:
                print(f"     content: {trim(evt.data.content)}")
                print(f"     (preceded by {counters['deltas']} delta events)")
            elif evt.type is SessionEventType.SESSION_ERROR:
                print(f"     ERROR: {evt.data.message}")
            waiter.handle(evt)

        session.on(on_event)
        async with session:
            await session.send(prompt)
            await waiter.wait()
            print(f"\nTotal delta events: {counters['deltas']}")
    return 0

