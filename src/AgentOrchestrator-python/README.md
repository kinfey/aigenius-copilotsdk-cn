# AgentOrchestrator Python

Python implementation of the AI Genius GitHub Copilot SDK lab.

```bash
uv sync
uv run pytest
uv run ruff check .
uv run uvicorn app.main:app --port 5070
```

Runnable SDK labs:

```bash
uv run python -m sdk_labs tools
uv run python -m sdk_labs events
uv run python -m sdk_labs sessions
uv run python -m sdk_labs mcp
```
