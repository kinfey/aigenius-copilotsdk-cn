# Agent HQ 零售分析前端

Blazor WebAssembly 零售分析助手，目标框架为 .NET 10。界面复刻本地参考应用 `http://127.0.0.1:5070/`：窄导航栏、助手欢迎气泡、五个推荐问题、语言选择和底部消息/模型输入区；沿用参考 CSS 及 640px、380px 响应式断点，没有引入其他前端框架。Markdown 使用 Markdig 与 HtmlSanitizer 两个 .NET 依赖。此项目只包含浏览器界面，不包含 Copilot SDK 凭据、服务端会话或零售数据。

## 本地运行

在仓库根目录运行：

```sh
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Web --launch-profile http
```

打开 `http://localhost:5051`。先启动独立后端；默认 API 地址为 `http://localhost:5050`，可以修改 `wwwroot/appsettings.json` 中的 `ApiBaseUrl`。后端必须允许前端来源 `http://localhost:5051` 的 CORS 请求，包括 `Content-Type` 请求头与 POST 方法。如果使用 HTTPS 前端，请同步配置 HTTPS API 和对应的 CORS 来源，以避免浏览器阻止混合内容。

## API 契约

- `GET /api/chat/models`：返回 `[{ "id": "...", "name": "..." }]`。界面只使用此列表中的模型；优先恢复仍可用的已保存模型，否则优先选择 `gpt-6-astra`，不存在时选择列表首项。列表为空或请求失败时，明确展示错误、重试按钮并禁用发送，不提供虚构的备选模型。
- `POST /api/chat/stream`：请求为 `{ "prompt": "...", "model": "...", "systemMessage": "..." }`，响应的 Content-Type 必须为 `text/event-stream`。
- SSE 事件以空行分隔；`data: {"content":"..."}` 为文本增量，`data: {"error":"..."}` 为错误，`data: [DONE]` 为正常结束。缺少结束标记而断开连接会显示错误并保留已收到内容。
- 每次提问独立发送，不上传浏览器对话历史。界面提示词要求按需调用只读零售 MCP 工具、不编造数据，并解释指标范围。**只读权限必须由后端强制实施，不能仅依赖提示词。**

## 浏览器行为

- 与参考一致，支持简体中文、繁体中文和 English，切换后同步界面、推荐问题及请求回答语言。推荐问题点击即发送；Enter 发送、Shift+Enter 换行，中文输入法组合输入期间 Enter 不发送。消息输入区自动增高至 120px。
- 导航栏“聊天 / 分析”和参考一样是展示按钮，不添加虚构的分析页面。欢迎气泡始终保留。生成期间发送按钮变为停止按钮，保留取消响应；禁止并发发送和切换模型。
- 使用参考的 Clawpilot 主题变量；页面最早执行的脚本读取 `?scoutTheme=light` / `?scoutTheme=dark`，未指定时跟随系统颜色偏好（浅色系统下与参考默认浅色一致）。旧版已保存的深色设置不再覆盖新界面；没有添加参考不存在的主题切换或清空按钮。
- 使用浏览器流式响应、UTF-8 流解码和 SSE 空行解析，适配任意网络分片、CRLF / LF、多行 `data:` 和注释心跳；以 75ms 周期批量更新界面。
- 助手回复通过 Markdig 渲染 Markdown，支持标题、粗体、列表、引用、代码块、链接和表格；流式增量与已保存的历史使用同一渲染逻辑。用户消息保持纯文本。禁用原始 HTML，并通过 HtmlSanitizer 白名单清理输出、移除图片与危险 URL，不执行模型返回的 HTML。宽表格和代码块在气泡内横向滚动。单次输入上限 12,000 字符，单次回复上限 100,000 字符。
- 模型、最近 100 条消息存储于当前浏览器来源的 localStorage，键名仍为 `agent-hq.retail.v1`，兼容旧记录。存储失败会明确提示。历史没有加密，不要输入敏感信息。若需重置历史，可在浏览器开发者工具的此键中将 `messages` 改为 `[]`，保留模型偏好；不要清除其他应用的存储。
- **浏览器历史不是 SDK 会话持久化**，也不会恢复工具上下文。SDK 会话恢复由独立控制台实验演示。

## 验证

```sh
dotnet build src/AgentOrchestrator/AgentHQDemo.Web/AgentHQDemo.Web.csproj
```

建议本地浏览器验证使用模拟 API，避免调用真实 AI：参考界面的桌面及移动端对比、三种语言、推荐问题与 Enter 行为、模型加载与失效模型修正、历史恢复、分片 UTF-8（含 emoji）、多行 SSE、服务端错误、连接截断、停止生成和并发保护。
