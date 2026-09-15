const key = "agent-hq.retail.v1";

export function load() {
    const value = localStorage.getItem(key);
    if (!value) return null;
    const state = JSON.parse(value);
    if (!state || typeof state !== "object" || !Array.isArray(state.messages)) {
        throw new Error("浏览器历史格式无效。可清空对话以重置。");
    }
    return {
        model: typeof state.model === "string" ? state.model : "",
        theme: state.theme === "light" ? "light" : "dark",
        messages: state.messages.filter(item => item &&
            ["user", "assistant"].includes(item.role) &&
            typeof item.content === "string" &&
            typeof item.createdAt === "string" &&
            !Number.isNaN(Date.parse(item.createdAt)))
            .slice(-100).map(item => ({
                role: item.role,
                content: item.content.slice(0, 100000),
                createdAt: item.createdAt,
                status: typeof item.status === "string" ? item.status : ""
            }))
    };
}

export function save(state) {
    localStorage.setItem(key, JSON.stringify({ ...state, messages: state.messages.slice(-100) }));
}

export function setLanguage(language) {
    document.documentElement.lang = language;
}

function resizeComposer() {
    const prompt = document.getElementById("prompt");
    if (!prompt) return;
    prompt.style.height = "auto";
    prompt.style.height = `${Math.min(prompt.scrollHeight, 120)}px`;
}

export function initializeComposer(language) {
    setLanguage(language);
    const prompt = document.getElementById("prompt");
    if (!prompt || prompt.dataset.initialized) return;
    prompt.dataset.initialized = "true";
    prompt.addEventListener("input", resizeComposer);
    prompt.addEventListener("keydown", event => {
        if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            document.getElementById("chat-form")?.requestSubmit();
        }
    });
    resizeComposer();
}

export function resetComposer() {
    const prompt = document.getElementById("prompt");
    if (prompt) prompt.value = "";
    resizeComposer();
}

export function focusComposer() {
    resizeComposer();
    document.getElementById("prompt")?.focus();
}

export function scrollChat(force) {
    const panel = document.getElementById("messages");
    if (!panel) return;
    if (force || panel.scrollHeight - panel.scrollTop - panel.clientHeight < 220) {
        panel.scrollTo({ top: panel.scrollHeight, behavior: "instant" });
    }
}
