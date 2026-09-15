# Labs 03–06：原创 .NET 控制台示例

这些示例按[中文实验目录](https://kinfey.github.io/aigenius-copilotsdk-cn/zh-cn/labs/)的学习顺序实现，**不是不可用的上游代码仓库的复制品**。仅使用 .NET 10 与固定版本 `GitHub.Copilot.SDK` **1.0.9**。

## 准备与离线验证

在仓库根目录执行：

```bash
dotnet build src/AgentOrchestrator/samples/SdkLabs
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- self-test
```

无参数、`--help` 和 `self-test` 不启动 Copilot CLI，不发送 AI 请求。自检无需额外测试框架，验证固定数据、工具描述、模型选择、参数校验、删除保护、权限策略和 MCP 成功证据。

AI 示例要求已安装并登录 Copilot CLI、有可用模型和网络访问权限；调用会消耗相应配额。使用已有 CLI，不下载另一份 CLI：

```bash
# 默认通过 PATH 查找 copilot；也可指定完整路径。
export COPILOT_CLI_PATH=/opt/homebrew/bin/copilot
```

SDK 1.0.9 的入口为 `RuntimeConnection.ForStdio(path: ...)`，不是旧版 `CliPath` 属性。项目使用仓库的 NuGet 源与 `CopilotSkipCliDownload` 设置。

每个 AI 命令先调用 `ListModelsAsync`。不传 `--model` 时优先选择实际列表中的 `claude-haiku-4.5`，否则选择首个模型（某些账户的首个模型是 `auto`）。指定不存在的 ID 会列出可用 ID 并以非零状态退出；不会静默换模型。

## Lab 03：自定义工具

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- tools
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- tools \
  --prompt "调用工具查询 C999，不要编造不存在的客户。"
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- tools \
  --expect-no-tools --prompt "In one short sentence, define average order value."
```

`DemoTools.cs` 使用 `CopilotTool.DefineTool`，方法与参数均有 `[Description]`：

- `get_customer_purchases`：C003 有两笔购买，Electronics 1200、Fashion 500，总额 **1700**。
- `get_customer_categories`：第二个工具／扩展练习，对同一固定数据按品类汇总。
- C999 返回 `Found=false` 与明确的 `not found` 错误，不伪装成零消费客户。

默认提示要求调用两个工具并查询未知客户。工具白名单只包含上述两个工具；没有文件读写、Shell 或任意宿主工具。至少一次客户工具成功完成才算工具示例成功。修改提示可以练习由模型选择一个或多个工具。

定义类问题不需要查询客户数据，可使用仅适用于 `tools` 的 `--expect-no-tools`。此模式保留相同的两个可选工具，验证模型确实选择不调用工具：必须有成功的非空回答，且实际 `ToolExecutionStartEvent` 工具调用数为 **0**，才返回 0；任何工具调用均失败。没有该标志时仍严格要求至少一个客户工具成功完成。

## Lab 04：事件与权限

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- events
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- events \
  --prompt "用三句话解释客户分群。"
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- permissions
```

`TurnRunner.cs` 在发送消息**之前**订阅 `On<SessionEvent>`：

1. `AssistantMessageDeltaEvent` 输出增量文本并计数。
2. `Stopwatch` 测量发送到首个非空文本增量的时间，不把推理事件当成首个文本 Token。
3. `AssistantUsageEvent` 累计实际收到的用量事件及输入／输出 Token 数；没有事件时明确提示数据缺失。它不是费用账单。
4. 收到 `SessionIdleEvent` 才结束；`SessionErrorEvent`、取消或超时会失败并尝试 `AbortAsync`。

`permissions` 仅暴露两个**无副作用、无敏感数据的内存工具**：

- `read_demo_summary`：批准一次。
- `read_demo_restricted`：拒绝；其实现也只是无害占位文本，绝不可替换成敏感操作。

打印真实权限回调次数、批准／拒绝次数及两个方法的执行次数。**零回调不代表权限策略得到执行**；如果限制工具的占位方法被执行，也会明确报告未阻止执行。模型可能不尝试两个工具，因此应检查实际计数，不能只看模型的自然语言结论。

SDK 1.0.9 的回调结果类型是 `GitHub.Copilot.Rpc.PermissionDecision`，该版本标记为实验性。只有使用它的源文件显式抑制 `GHCP001`；不假设新版 API 与此相同。

## Lab 05：会话持久化与恢复

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- sessions
```

命令生成唯一的 `sdklabs-<guid>` ID，要求模型在当前对话记住 **At Risk**，释放原会话，然后使用 `ResumeSessionConfig` 恢复并追问。恢复提示不包含正确答案；未回忆出预期标签则失败。最后只通过 `GetSessionMetadataAsync(id)` 读取这个会话的元数据。

把终端输出中的实际 ID 代入以下命令，可验证**跨进程恢复**及显式清理：

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- sessions \
  --resume sdklabs-REPLACE-WITH-PRINTED-ID
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- sessions \
  --delete sdklabs-REPLACE-WITH-PRINTED-ID
```

不枚举个人会话。恢复／删除仅接受明确指定、由 ASCII 字母、数字和连字符组成的 `sdklabs-*` ID；拒绝路径、其他前缀、同时恢复和删除等参数。删除不调用模型，也不进行批量清理。示例会话保留在 CLI 的本地会话存储中，直到显式删除。

## Lab 06：Microsoft Learn MCP

```bash
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- mcp
dotnet run --project src/AgentOrchestrator/samples/SdkLabs --no-build -- mcp \
  --prompt "必须调用 Microsoft Learn MCP 搜索 ASP.NET Core 依赖注入文档，简短总结并提供来源链接。"
```

使用强类型 `McpHttpServerConfig`：

- URL：`https://learn.microsoft.com/api/mcp`
- 服务器名：`microsoft-learn`
- 明确设置 `Tools = ["*"]`，避免不同运行时对缺省工具配置处理不一致。
- 会话白名单只允许三个限定名：`microsoft-learn-microsoft_docs_search`、`microsoft-learn-microsoft_docs_fetch`、`microsoft-learn-microsoft_code_sample_search`。
- 权限回调只批准该服务器的对应只读 MCP 工具。

**不是看到答案就成功**：检查实际 `ToolExecutionStartEvent.Data.McpServerName` 和 `McpToolName`，按 `ToolCallId` 匹配成功的 `ToolExecutionCompleteEvent`。必须至少有一次成功调用、所有已观察到的 Learn 调用均成功完成、且无失败，才输出 `Verified Microsoft Learn MCP` 并返回 0。内置 `web_fetch`、其他服务器、仅开始未完成、没有 MCP 调用或调用出错均不能冒充成功；离线时不会退化成凭记忆回答的“成功”。

## 错误与边界

- `--timeout 1` 至 `--timeout 600` 设置整体执行超时（默认 120 秒）；Ctrl+C 同样取消。失败写入 stderr 并返回 1。
- 取消时另有最多 5 秒的中止请求；会话与客户端还需释放资源。
- 自定义 `--prompt` 仅支持 `tools`、`events`、`mcp`。
- 所有会话禁用配置发现、文件 hooks、skills、宿主 Git 操作和自动发现指令；没有全局 `ApproveAll`。
- 模型输出和 Token 数会变化。白名单及真实事件证据比自然语言声称更可信。

## 版本依据

API 已通过实际还原的 NuGet 1.0.9 `net10.0` 程序集及 XML 文档编译验证；该包的 `.nuspec` 记录源码提交
[`cc8c7f28ea9f1894fe91a2cf161e2e6f8a52ff51`](https://github.com/github/copilot-sdk/tree/cc8c7f28ea9f1894fe91a2cf161e2e6f8a52ff51/dotnet)。
可参阅同版本的 [`Client.cs`](https://github.com/github/copilot-sdk/blob/v1.0.9/dotnet/src/Client.cs)、[`Session.cs`](https://github.com/github/copilot-sdk/blob/v1.0.9/dotnet/src/Session.cs) 和 [`Types.cs`](https://github.com/github/copilot-sdk/blob/v1.0.9/dotnet/src/Types.cs)，不要直接照搬 `main` 或旧版示例。
