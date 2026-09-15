from __future__ import annotations

import asyncio

from copilot.session_events import SessionEvent, SessionEventType

TIMEOUT_SECONDS = 180.0


class IdleWaiter:
    def __init__(self) -> None:
        self._future: asyncio.Future[None] = asyncio.get_event_loop().create_future()

    def handle(self, evt: SessionEvent) -> bool:
        if evt.type is SessionEventType.SESSION_IDLE:
            if not self._future.done():
                self._future.set_result(None)
            return True
        if evt.type is SessionEventType.SESSION_ERROR:
            if not self._future.done():
                self._future.set_exception(RuntimeError(evt.data.message))
            return True
        return False

    async def wait(self) -> None:
        await asyncio.wait_for(self._future, timeout=TIMEOUT_SECONDS)


def trim(value: str | None, length: int = 100) -> str:
    text = (value or "").replace("\n", " ").strip()
    return text if len(text) <= length else text[: length - 1] + "…"


def message_content(result: object) -> str:
    data = getattr(result, "data", result)
    return str(getattr(data, "content", "") or "")

