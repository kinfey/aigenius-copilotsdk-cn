import json

from fastapi import APIRouter
from fastapi.responses import StreamingResponse

from app.models import ChatRequest, ModelRead
from app.services.copilot_chat import FALLBACK_MODELS, CopilotChatService

router = APIRouter(prefix="/api/chat", tags=["chat"])
service = CopilotChatService()


@router.get("/health")
def health():
    return {
        "status": "healthy",
        "service": "CopilotChat",
        "availableModels": [model["id"] for model in FALLBACK_MODELS],
    }


@router.get("/models", response_model=list[ModelRead])
async def models():
    return await service.list_models()


@router.post("")
async def chat(request: ChatRequest):
    return {
        "content": await service.chat(
            prompt=request.prompt,
            model=request.model,
            system_message=request.system_message,
        )
    }


@router.post("/stream")
def stream_chat(request: ChatRequest):
    async def event_stream():
        try:
            async for chunk in service.chat_stream(
                prompt=request.prompt,
                model=request.model,
                system_message=request.system_message,
            ):
                yield f"data: {json.dumps({'content': chunk})}\n\n"
        except Exception as exc:
            yield f"data: {json.dumps({'error': str(exc)})}\n\n"
        yield "data: [DONE]\n\n"

    return StreamingResponse(event_stream(), media_type="text/event-stream")
