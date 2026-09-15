import uuid

from copilot import CopilotClient
from copilot.session_events import SessionEventType

from sdk_labs import model_picker
from sdk_labs._common import IdleWaiter


async def send_and_print(session, prompt: str) -> None:
    waiter = IdleWaiter()

    def on_event(evt) -> None:
        if evt.type is SessionEventType.ASSISTANT_MESSAGE and evt.data.content:
            print(f"Assistant: {evt.data.content}")
        waiter.handle(evt)

    session.on(on_event)
    print(f"You: {prompt}")
    await session.send(prompt)
    await waiter.wait()


async def run(requested_model_id: str | None = None, resume_id: str | None = None) -> int:
    print("== Lab 05: sessions ==\n")
    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1
        print(f"Model: {model_id}")
        if resume_id:
            print(f"Resuming session: {resume_id}\n")
            resumed = await client.resume_session(resume_id, model=model_id, streaming=False)
            async with resumed:
                await send_and_print(
                    resumed, "Which retail segment did I say was my favourite?"
                )
            return 0

        session_id = f"sdklabs-{uuid.uuid4().hex}"[:24]
        print(f"Session id: {session_id}\n")
        print("--- Turn 1 (new session) ---")
        session = await client.create_session(
            session_id=session_id,
            model=model_id,
            streaming=False,
        )
        async with session:
            await send_and_print(
                session,
                "Remember this: my favourite retail segment is 'At Risk'. Reply with just OK.",
            )
        print("\nSession closed.\n")
        print("--- Turn 2 (resumed session) ---")
        resumed = await client.resume_session(session_id, model=model_id, streaming=False)
        async with resumed:
            await send_and_print(
                resumed, "Which retail segment did I say was my favourite?"
            )
        print("\n--- Session metadata ---")
        metadata = await client.get_session_metadata(session_id)
        print(
            "  (no metadata returned)"
            if metadata is None
            else f"  id={session_id} metadata retrieved"
        )
    return 0

