from pathlib import Path

from fastapi.testclient import TestClient

from app.main import app
from app.models import ChatRequest, SegmentPrediction


def test_chat_request_uses_prompt_field():
    request = ChatRequest.model_validate({"prompt": "hello", "model": "gpt-5"})
    assert request.prompt == "hello"


def test_prediction_serializes_camel_case():
    prediction = SegmentPrediction(
        customer_id="C003",
        predicted_segment="High Value",
        confidence=0.89,
        top_features=["total_1700"],
    )
    assert prediction.model_dump(by_alias=True)["predictedSegment"] == "High Value"


def test_static_ui_posts_prompt_and_model():
    app_js = Path("app/static/app.js").read_text()
    index_html = Path("app/static/index.html").read_text()
    assert "systemMessage: content.systemMessage" in app_js
    assert "JSON.stringify({message, model})" not in app_js
    assert '"zh-CN"' in app_js
    assert '"zh-TW"' in app_js
    assert "Always answer in English" in app_js
    assert index_html.count("data-prompt=") == 5
    assert 'id="language"' in index_html
    assert 'class="teams-rail"' in index_html
    assert 'id="nav-chat"' in index_html
    assert 'class="chat-header"' in index_html
    assert 'class="composer"' in index_html
    assert 'class="composer-main"' in index_html
    assert index_html.index('id="model-status"') > index_html.index('id="prompt"')
    assert "viewport-fit=cover" in index_html
    assert 'setAttribute("data-theme", "light")' in index_html
    assert index_html.index('class="model-picker"') > index_html.index('class="composer"')
    assert 'classList.add("pending-bubble")' in app_js
    assert 'classList.remove("pending-bubble")' in app_js
    assert 'label.textContent = locale().typing' in app_js
    assert 'dot.className = "typing-dot"' in app_js
    assert "function renderMarkdown(markdown)" in app_js
    assert "assistant.content.innerHTML = renderMarkdown(answer)" in app_js
    assert "escapeHtml(value)" in app_js
    assert 'class="markdown-table-wrap"' in app_js
    assert "<thead><tr>" in app_js
    style_css = Path("app/static/style.css").read_text()
    assert "background: var(--cp-success);" in style_css
    assert "border-color: var(--cp-success);" in style_css


def test_health_contract():
    with TestClient(app) as client:
        response = client.get("/api/chat/health")
    assert response.status_code == 200
    assert response.json()["service"] == "CopilotChat"
