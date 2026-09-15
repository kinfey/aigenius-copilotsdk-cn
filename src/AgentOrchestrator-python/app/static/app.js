const form = document.querySelector("#chat-form");
const promptInput = document.querySelector("#prompt");
const modelSelect = document.querySelector("#model");
const languageSelect = document.querySelector("#language");
const modelStatus = document.querySelector("#model-status");
const messages = document.querySelector("#messages");
const sendButton = document.querySelector("#send-button");
const suggestionButtons = document.querySelectorAll("[data-suggestion-index]");

const translations = {
  "zh-CN": {
    title: "零售分析助手",
    navChat: "聊天",
    navAnalytics: "分析",
    assistantStatus: "Copilot 助手",
    connecting: "正在连接可用模型...",
    modelsAvailable: (count) => `已有 ${count} 个模型可用`,
    modelFailure: (message) => `模型发现失败，使用 GPT-6 Astra（${message}）`,
    today: "今天",
    welcome:
      "你好！我可以协助你分析客户交易、客户分群、留存率和零售 KPI。",
    suggestionTitle: "你可以这样问：",
    now: "现在",
    message: "消息",
    placeholder: "输入消息",
    model: "模型",
    chooseModel: "选择模型",
    language: "语言",
    chooseLanguage: "选择语言",
    send: "发送消息",
    typing: "助手正在输入",
    requestFailure: (status) => `聊天请求失败，HTTP 状态码：${status}`,
    error: (message) => `抱歉，无法完成这个请求：${message}`,
    systemMessage:
      "请始终使用简体中文回答。回答应清晰、简洁，并优先使用可用的零售分析工具获取事实。",
    suggestions: [
      {
        label: "C003 的总消费和客户分群是什么？",
        prompt: "客户 C003 的总消费金额是多少？属于哪个客户分群？",
      },
      {
        label: "哪个客户分群的留存率最低？",
        prompt: "哪个客户分群的留存率最低？",
      },
      {
        label: "查看 C003 最近的购买记录",
        prompt: "显示客户 C003 最近的购买记录和商品类别。",
      },
      {
        label: "比较各客户分群的留存率",
        prompt: "比较所有客户分群的留存率。",
      },
      {
        label: "列出三个重要的零售 KPI",
        prompt: "列出三个重要的零售 KPI，每行一个。",
      },
    ],
  },
  "zh-TW": {
    title: "零售分析助理",
    navChat: "聊天",
    navAnalytics: "分析",
    assistantStatus: "Copilot 助理",
    connecting: "正在連線可用模型...",
    modelsAvailable: (count) => `已有 ${count} 個模型可用`,
    modelFailure: (message) => `模型探索失敗，使用 GPT-6 Astra（${message}）`,
    today: "今天",
    welcome:
      "你好！我可以協助你分析客戶交易、客戶分群、留存率和零售 KPI。",
    suggestionTitle: "你可以這樣問：",
    now: "現在",
    message: "訊息",
    placeholder: "輸入訊息",
    model: "模型",
    chooseModel: "選擇模型",
    language: "語言",
    chooseLanguage: "選擇語言",
    send: "傳送訊息",
    typing: "助理正在輸入",
    requestFailure: (status) => `聊天請求失敗，HTTP 狀態碼：${status}`,
    error: (message) => `抱歉，無法完成這個請求：${message}`,
    systemMessage:
      "請始終使用繁體中文回答。回答應清晰、簡潔，並優先使用可用的零售分析工具取得事實。",
    suggestions: [
      {
        label: "C003 的總消費和客戶分群是什麼？",
        prompt: "客戶 C003 的總消費金額是多少？屬於哪個客戶分群？",
      },
      {
        label: "哪個客戶分群的留存率最低？",
        prompt: "哪個客戶分群的留存率最低？",
      },
      {
        label: "查看 C003 最近的購買紀錄",
        prompt: "顯示客戶 C003 最近的購買紀錄和商品類別。",
      },
      {
        label: "比較各客戶分群的留存率",
        prompt: "比較所有客戶分群的留存率。",
      },
      {
        label: "列出三個重要的零售 KPI",
        prompt: "列出三個重要的零售 KPI，每行一個。",
      },
    ],
  },
  en: {
    title: "Retail Analytics Assistant",
    navChat: "Chat",
    navAnalytics: "Analytics",
    assistantStatus: "Copilot assistant",
    connecting: "Connecting to available models...",
    modelsAvailable: (count) => `${count} models available`,
    modelFailure: (message) =>
      `Model discovery failed; using GPT-6 Astra (${message})`,
    today: "Today",
    welcome:
      "Hello! I can help you explore customer transactions, segments, retention, and retail KPIs.",
    suggestionTitle: "Try one of these:",
    now: "Now",
    message: "Message",
    placeholder: "Message",
    model: "Model",
    chooseModel: "Choose model",
    language: "Language",
    chooseLanguage: "Choose language",
    send: "Send message",
    typing: "Assistant is typing",
    requestFailure: (status) => `Chat request failed with HTTP ${status}`,
    error: (message) => `Sorry, I couldn't complete that request: ${message}`,
    systemMessage:
      "Always answer in English. Be clear and concise, and prefer the available retail analytics tools for factual answers.",
    suggestions: [
      {
        label: "What is C003's total spend and segment?",
        prompt: "What is customer C003 total spend and which segment are they in?",
      },
      {
        label: "Which segment has the lowest retention?",
        prompt: "Which customer segment has the lowest retention rate?",
      },
      {
        label: "Show C003's recent purchases",
        prompt: "Show the recent purchases and product categories for customer C003.",
      },
      {
        label: "Compare segment retention rates",
        prompt: "Compare the retention rates of all customer segments.",
      },
      {
        label: "List three retail KPIs",
        prompt: "List three important retail KPIs, one per line.",
      },
    ],
  },
};

let modelLoadState = {status: "loading", count: 0, error: ""};

function locale() {
  return translations[languageSelect.value] || translations["zh-CN"];
}

function updateModelStatus() {
  const content = locale();
  if (modelLoadState.status === "loaded") {
    modelStatus.textContent = content.modelsAvailable(modelLoadState.count);
  } else if (modelLoadState.status === "failed") {
    modelStatus.textContent = content.modelFailure(modelLoadState.error);
  } else {
    modelStatus.textContent = content.connecting;
  }
}

function applyLocale() {
  const content = locale();
  document.documentElement.lang = languageSelect.value;
  document.title = content.title;
  document.querySelector("#nav-chat").textContent = content.navChat;
  document.querySelector("#nav-analytics").textContent = content.navAnalytics;
  document.querySelector("#contact-title").textContent = content.title;
  document.querySelector("#assistant-status").textContent = content.assistantStatus;
  document.querySelector("#today-label").textContent = content.today;
  document.querySelector("#welcome-author").textContent = content.title;
  document.querySelector("#welcome-text").textContent = content.welcome;
  document.querySelector("#suggestion-title").textContent = content.suggestionTitle;
  document.querySelector("#now-label").textContent = content.now;
  document.querySelector("#prompt-label").textContent = content.message;
  document.querySelector("#model-label").textContent = content.model;
  promptInput.placeholder = content.placeholder;
  modelSelect.setAttribute("aria-label", content.chooseModel);
  languageSelect.setAttribute("aria-label", content.chooseLanguage);
  sendButton.setAttribute("aria-label", content.send);
  suggestionButtons.forEach((button, index) => {
    const suggestion = content.suggestions[index];
    button.textContent = suggestion.label;
    button.dataset.prompt = suggestion.prompt;
  });
  document.querySelector("#suggestions").setAttribute(
    "aria-label",
    content.suggestionTitle,
  );
  updateModelStatus();
}

function currentTime() {
  return new Intl.DateTimeFormat(languageSelect.value, {
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date());
}

function scrollToLatest() {
  messages.scrollTop = messages.scrollHeight;
}

function escapeHtml(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function renderInlineMarkdown(value) {
  let rendered = escapeHtml(value);
  rendered = rendered.replace(/`([^`]+)`/g, "<code>$1</code>");
  rendered = rendered.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  rendered = rendered.replace(/__([^_]+)__/g, "<strong>$1</strong>");
  rendered = rendered.replace(/\*([^*]+)\*/g, "<em>$1</em>");
  rendered = rendered.replace(
    /\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g,
    '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>',
  );
  return rendered;
}

function renderMarkdown(markdown) {
  const lines = markdown.replaceAll("\r\n", "\n").split("\n");
  const output = [];
  let listType = null;
  let inCodeBlock = false;
  let codeLines = [];

  function closeList() {
    if (listType) {
      output.push(`</${listType}>`);
      listType = null;
    }
  }

  function tableCells(line) {
    return line
      .trim()
      .replace(/^\|/, "")
      .replace(/\|$/, "")
      .split("|")
      .map((cell) => cell.trim());
  }

  for (let lineIndex = 0; lineIndex < lines.length; lineIndex += 1) {
    const line = lines[lineIndex];
    if (line.trim().startsWith("```")) {
      closeList();
      if (inCodeBlock) {
        output.push(`<pre><code>${escapeHtml(codeLines.join("\n"))}</code></pre>`);
        codeLines = [];
      }
      inCodeBlock = !inCodeBlock;
      continue;
    }

    if (inCodeBlock) {
      codeLines.push(line);
      continue;
    }

    const nextLine = lines[lineIndex + 1] || "";
    const headerCells = tableCells(line);
    const separatorCells = tableCells(nextLine);
    const isTable =
      line.includes("|") &&
      headerCells.length > 1 &&
      headerCells.length === separatorCells.length &&
      separatorCells.every((cell) => /^:?-{3,}:?$/.test(cell));
    if (isTable) {
      closeList();
      const header = headerCells
        .map((cell) => `<th>${renderInlineMarkdown(cell)}</th>`)
        .join("");
      const rows = [];
      lineIndex += 1;
      while (
        lineIndex + 1 < lines.length &&
        lines[lineIndex + 1].trim() &&
        lines[lineIndex + 1].includes("|")
      ) {
        lineIndex += 1;
        const cells = tableCells(lines[lineIndex])
          .map((cell) => `<td>${renderInlineMarkdown(cell)}</td>`)
          .join("");
        rows.push(`<tr>${cells}</tr>`);
      }
      output.push(
        `<div class="markdown-table-wrap"><table><thead><tr>${header}</tr></thead>` +
          `<tbody>${rows.join("")}</tbody></table></div>`,
      );
      continue;
    }

    const heading = line.match(/^(#{1,3})\s+(.+)$/);
    if (heading) {
      closeList();
      const level = heading[1].length;
      output.push(`<h${level}>${renderInlineMarkdown(heading[2])}</h${level}>`);
      continue;
    }

    const unordered = line.match(/^\s*[-*]\s+(.+)$/);
    if (unordered) {
      if (listType !== "ul") {
        closeList();
        listType = "ul";
        output.push("<ul>");
      }
      output.push(`<li>${renderInlineMarkdown(unordered[1])}</li>`);
      continue;
    }

    const ordered = line.match(/^\s*\d+[.)]\s+(.+)$/);
    if (ordered) {
      if (listType !== "ol") {
        closeList();
        listType = "ol";
        output.push("<ol>");
      }
      output.push(`<li>${renderInlineMarkdown(ordered[1])}</li>`);
      continue;
    }

    closeList();
    if (!line.trim()) {
      output.push("");
    } else if (line.startsWith("> ")) {
      output.push(`<blockquote>${renderInlineMarkdown(line.slice(2))}</blockquote>`);
    } else {
      output.push(`<p>${renderInlineMarkdown(line)}</p>`);
    }
  }

  closeList();
  if (inCodeBlock && codeLines.length) {
    output.push(`<pre><code>${escapeHtml(codeLines.join("\n"))}</code></pre>`);
  }
  return output.join("");
}

function createMessageRow(role, text = "") {
  const row = document.createElement("article");
  row.className = `message-row ${role}-row`;

  const bubble = document.createElement("div");
  bubble.className = `message-bubble ${role}-bubble`;

  const content = document.createElement("div");
  content.className = "message-content";
  content.textContent = text;
  bubble.appendChild(content);

  const time = document.createElement("time");
  time.className = "message-time";
  time.textContent = currentTime();
  bubble.appendChild(time);

  row.appendChild(bubble);
  messages.appendChild(row);
  scrollToLatest();
  return {row, bubble, content};
}

function showTyping(content) {
  content.closest(".message-bubble").classList.add("pending-bubble");
  content.replaceChildren();
  const typing = document.createElement("span");
  typing.className = "typing";
  typing.setAttribute("aria-label", locale().typing);
  const label = document.createElement("span");
  label.className = "typing-label";
  label.textContent = locale().typing;
  typing.appendChild(label);
  for (let index = 0; index < 3; index += 1) {
    const dot = document.createElement("span");
    dot.className = "typing-dot";
    typing.appendChild(dot);
  }
  content.appendChild(typing);
}

function resizeComposer() {
  promptInput.style.height = "auto";
  promptInput.style.height = `${Math.min(promptInput.scrollHeight, 120)}px`;
}

async function loadModels() {
  const selectedModel = modelSelect.value || "gpt-6-astra";
  try {
    const response = await fetch("/api/chat/models");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const models = await response.json();
    modelSelect.replaceChildren();
    for (const model of models) {
      const option = document.createElement("option");
      option.value = model.id;
      option.textContent = model.name;
      modelSelect.appendChild(option);
    }
    modelSelect.value = models.some((model) => model.id === selectedModel)
      ? selectedModel
      : models[0]?.id || "gpt-6-astra";
    modelLoadState = {status: "loaded", count: models.length, error: ""};
  } catch (error) {
    modelLoadState = {status: "failed", count: 0, error: error.message};
  }
  updateModelStatus();
}

async function sendMessage(prompt) {
  const cleanPrompt = prompt.trim();
  if (!cleanPrompt || sendButton.disabled) return;

  const content = locale();
  const model = modelSelect.value;
  createMessageRow("user", cleanPrompt);
  const assistant = createMessageRow("assistant");
  showTyping(assistant.content);

  promptInput.value = "";
  resizeComposer();
  sendButton.disabled = true;

  try {
    const response = await fetch("/api/chat/stream", {
      method: "POST",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({
        prompt: cleanPrompt,
        model,
        systemMessage: content.systemMessage,
      }),
    });
    if (!response.ok || !response.body) {
      throw new Error(content.requestFailure(response.status));
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    let answer = "";

    while (true) {
      const {value, done} = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, {stream: true});
      const frames = buffer.split("\n\n");
      buffer = frames.pop();
      for (const frame of frames) {
        if (!frame.startsWith("data: ")) continue;
        const data = frame.slice(6);
        if (data === "[DONE]") continue;
        const payload = JSON.parse(data);
        if (payload.error) throw new Error(payload.error);
        answer += payload.content || "";
        assistant.bubble.classList.remove("pending-bubble");
        assistant.content.innerHTML = renderMarkdown(answer);
        scrollToLatest();
      }
    }
  } catch (error) {
    assistant.bubble.classList.remove("pending-bubble");
    assistant.content.textContent = content.error(error.message);
  } finally {
    assistant.bubble.classList.remove("pending-bubble");
    sendButton.disabled = false;
    promptInput.focus();
    scrollToLatest();
  }
}

form.addEventListener("submit", (event) => {
  event.preventDefault();
  sendMessage(promptInput.value);
});

languageSelect.addEventListener("change", applyLocale);
promptInput.addEventListener("input", resizeComposer);
promptInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter" && !event.shiftKey) {
    event.preventDefault();
    form.requestSubmit();
  }
});

for (const button of suggestionButtons) {
  button.addEventListener("click", () => {
    sendMessage(button.dataset.prompt);
  });
}

applyLocale();
loadModels();
resizeComposer();
