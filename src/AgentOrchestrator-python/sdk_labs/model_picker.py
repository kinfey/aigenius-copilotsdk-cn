from __future__ import annotations

PREFERRED_MODEL = "gpt-6-astra"


async def pick(client, requested_model_id: str | None = None) -> str | None:
    try:
        models = await client.list_models()
    except Exception as exc:
        if requested_model_id:
            return requested_model_id
        print(f"Model discovery warning: {exc}")
        return PREFERRED_MODEL

    ids = [model.id for model in models]
    if requested_model_id:
        if requested_model_id not in ids:
            print(f"Requested model is unavailable: {requested_model_id}")
            return None
        return requested_model_id
    if PREFERRED_MODEL in ids:
        return PREFERRED_MODEL
    return ids[0] if ids else None
