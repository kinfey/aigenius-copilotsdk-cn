from __future__ import annotations

import asyncio
import logging
import sys
from collections.abc import AsyncIterator
from pathlib import Path

from copilot import CopilotClient
from copilot.session import PermissionHandler
from copilot.session_events import SessionEvent, SessionEventType

logger = logging.getLogger(__name__)
PROJECT_ROOT = Path(__file__).resolve().parents[2]

FALLBACK_MODELS = [
    {"id": "gpt-6-astra", "name": "GPT-6 Astra", "description": "Preferred lab model"},
    {"id": "claude-haiku-4.5", "name": "Claude Haiku 4.5", "description": "Fast Claude model"},
    {"id": "gpt-4.1", "name": "GPT-4.1", "description": "OpenAI general model"},
    {"id": "gpt-5", "name": "GPT-5", "description": "OpenAI reasoning model"},
    {"id": "claude-sonnet-4.5", "name": "Claude Sonnet 4.5", "description": ""},
    {"id": "claude-opus-4.5", "name": "Claude Opus 4.5", "description": ""},
    {"id": "gemini-2.5-pro", "name": "Gemini 2.5 Pro", "description": ""},
]


class CopilotChatService:
    async def list_models(self) -> list[dict[str, str]]:
        try:
            async with CopilotClient(working_directory=str(PROJECT_ROOT)) as client:
                models = await client.list_models()
                return [
                    {
                        "id": model.id,
                        "name": model.name or model.id,
                        "description": getattr(model, "description", "") or "",
                    }
                    for model in models
                ]
        except Exception:
            logger.exception("Live model discovery failed; using fallback catalog")
            return FALLBACK_MODELS

    async def chat_stream(
        self, prompt: str, model: str, system_message: str | None = None
    ) -> AsyncIterator[str]:
        queue: asyncio.Queue[str] = asyncio.Queue()
        done = asyncio.get_running_loop().create_future()
        mcp_servers = {
            "retail-analytics": {
                "command": sys.executable,
                "args": ["-m", "mcp_server"],
                "working_directory": str(PROJECT_ROOT),
                "tools": ["*"],
            }
        }
        async with CopilotClient(working_directory=str(PROJECT_ROOT)) as client:
            session = await client.create_session(
                model=model,
                streaming=True,
                system_message=(
                    {"mode": "append", "content": system_message} if system_message else None
                ),
                mcp_servers=mcp_servers,
                on_permission_request=PermissionHandler.approve_all,
            )

            def on_event(evt: SessionEvent) -> None:
                if evt.type is SessionEventType.ASSISTANT_MESSAGE_DELTA:
                    queue.put_nowait(evt.data.delta_content or "")
                elif evt.type is SessionEventType.ASSISTANT_MESSAGE:
                    logger.info(
                        "Assistant response complete: %d chars",
                        len(evt.data.content or ""),
                    )
                elif evt.type is SessionEventType.TOOL_EXECUTION_START:
                    if getattr(evt.data, "mcp_server_name", None):
                        logger.info(
                            "MCP tool call: %s/%s",
                            evt.data.mcp_server_name,
                            evt.data.tool_name,
                        )
                elif evt.type is SessionEventType.SESSION_IDLE:
                    if not done.done():
                        done.set_result(None)
                elif evt.type is SessionEventType.SESSION_ERROR and not done.done():
                    done.set_exception(RuntimeError(evt.data.message))

            session.on(on_event)
            async with session:
                await session.send(prompt)
                while not done.done() or not queue.empty():
                    try:
                        chunk = await asyncio.wait_for(queue.get(), timeout=0.1)
                    except TimeoutError:
                        continue
                    if chunk:
                        yield chunk
                await done

    async def chat(self, prompt: str, model: str, system_message: str | None = None) -> str:
        return "".join(
            [
                chunk
                async for chunk in self.chat_stream(
                    prompt=prompt,
                    model=model,
                    system_message=system_message,
                )
            ]
        )
