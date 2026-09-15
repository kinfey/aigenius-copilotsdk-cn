import os

from copilot import CopilotClient
from copilot.session import PermissionHandler
from copilot.session_events import SessionEventType

from sdk_labs import model_picker
from sdk_labs._common import IdleWaiter


async def run(requested_model_id: str | None = None) -> int:
    print("== Lab 06: mcp ==\n")
    prompt = (
        "Use Microsoft Learn tools to explain Azure Container Apps in two sentences. "
        "You must consult the documentation before answering."
    )
    async with CopilotClient() as client:
        model_id = await model_picker.pick(client, requested_model_id)
        if model_id is None:
            return 1
        print(f"Model: {model_id}")
        print("Prompt: asking the model to consult Microsoft Learn docs\n")
        waiter = IdleWaiter()
        invoked_tools: set[str] = set()
        mcp_tools: set[str] = set()
        tool_calls: dict[str, str] = {}
        session = await client.create_session(
            model=model_id,
            streaming=False,
            mcp_servers={
                "microsoft.docs.mcp": {
                    "type": "http",
                    "url": "https://learn.microsoft.com/api/mcp",
                    "tools": ["*"],
                }
            },
            on_permission_request=PermissionHandler.approve_all,
        )

        def on_event(evt) -> None:
            if os.getenv("SDKLABS_TRACE_EVENTS") == "1":
                print(f"  [event] {evt.type.value}")
            if (
                evt.type.value.startswith("session.mcp")
                or evt.type.value == "mcp.tools.list_changed"
            ):
                print(f"  [mcp] {evt.type.value}")
            if evt.type is SessionEventType.TOOL_EXECUTION_START:
                tool_name = evt.data.tool_name
                tool_calls[evt.data.tool_call_id] = tool_name
                invoked_tools.add(tool_name)
                print(f"  [tool] {evt.type.value}: {tool_name}")
                if evt.data.mcp_server_name:
                    mcp_tools.add(tool_name)
            elif evt.type is SessionEventType.TOOL_EXECUTION_COMPLETE:
                tool_name = tool_calls.get(evt.data.tool_call_id, evt.data.tool_call_id)
                print(
                    f"  [tool] {evt.type.value}: {tool_name} "
                    f"(success={evt.data.success})"
                )
            elif evt.type is SessionEventType.ASSISTANT_MESSAGE and evt.data.content:
                print(f"\nAssistant: {evt.data.content}")
            waiter.handle(evt)

        session.on(on_event)
        async with session:
            await session.send(prompt)
            await waiter.wait()

    print()
    if not mcp_tools:
        print("WARNING: No MCP tool was invoked.")
        if invoked_tools:
            print(f"Non-MCP tools used: {', '.join(sorted(invoked_tools))}")
        return 1
    print(f"MCP tool(s) invoked: {', '.join(sorted(mcp_tools))}")
    return 0
