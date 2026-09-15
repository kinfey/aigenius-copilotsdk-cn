namespace AgentHQDemo.Web.Services;

internal sealed record ChatLocale(
    string Title, string NavChat, string NavAnalytics, string AssistantStatus,
    string Connecting, string Today, string Welcome, string SuggestionTitle, string Now,
    string Message, string Placeholder, string Model, string ChooseModel,
    string Language, string ChooseLanguage, string Send, string Typing,
    string SystemMessage, string ModelFailure, string Error, string Stop, string Retry, string NoModels,
    (string Label, string Prompt)[] Suggestions)
{
    public string ModelsAvailable(int count) => this == English ? $"{count} models available"
        : this == Traditional ? $"已有 {count} 個模型可用" : $"已有 {count} 个模型可用";

    public static ChatLocale For(string language) => language switch
    {
        "zh-TW" => Traditional,
        "en" => English,
        _ => Simplified
    };

    private static readonly ChatLocale Simplified = new(
        "零售分析助手", "聊天", "分析", "Copilot 助手",
        "正在连接可用模型...", "今天",
        "你好！我可以协助你分析客户交易、客户分群、留存率和零售 KPI。",
        "你可以这样问：", "现在", "消息", "输入消息", "模型", "选择模型", "语言", "选择语言",
        "发送消息", "助手正在输入",
        "请始终使用简体中文回答。回答应清晰、简洁，并优先使用可用的零售分析工具获取事实。",
        "模型发现失败：", "抱歉，无法完成这个请求：", "停止生成", "重试", "暂无可用模型",
        [
            ("C003 的总消费和客户分群是什么？", "客户 C003 的总消费金额是多少？属于哪个客户分群？"),
            ("哪个客户分群的留存率最低？", "哪个客户分群的留存率最低？"),
            ("查看 C003 最近的购买记录", "显示客户 C003 最近的购买记录和商品类别。"),
            ("比较各客户分群的留存率", "比较所有客户分群的留存率。"),
            ("列出三个重要的零售 KPI", "列出三个重要的零售 KPI，每行一个。")
        ]);

    private static readonly ChatLocale Traditional = new(
        "零售分析助理", "聊天", "分析", "Copilot 助理",
        "正在連線可用模型...", "今天",
        "你好！我可以協助你分析客戶交易、客戶分群、留存率和零售 KPI。",
        "你可以這樣問：", "現在", "訊息", "輸入訊息", "模型", "選擇模型", "語言", "選擇語言",
        "傳送訊息", "助理正在輸入",
        "請始終使用繁體中文回答。回答應清晰、簡潔，並優先使用可用的零售分析工具取得事實。",
        "模型探索失敗：", "抱歉，無法完成這個請求：", "停止生成", "重試", "暫無可用模型",
        [
            ("C003 的總消費和客戶分群是什麼？", "客戶 C003 的總消費金額是多少？屬於哪個客戶分群？"),
            ("哪個客戶分群的留存率最低？", "哪個客戶分群的留存率最低？"),
            ("查看 C003 最近的購買紀錄", "顯示客戶 C003 最近的購買紀錄和商品類別。"),
            ("比較各客戶分群的留存率", "比較所有客戶分群的留存率。"),
            ("列出三個重要的零售 KPI", "列出三個重要的零售 KPI，每行一個。")
        ]);

    private static readonly ChatLocale English = new(
        "Retail Analytics Assistant", "Chat", "Analytics", "Copilot assistant",
        "Connecting to available models...", "Today",
        "Hello! I can help you explore customer transactions, segments, retention, and retail KPIs.",
        "Try one of these:", "Now", "Message", "Message", "Model", "Choose model", "Language", "Choose language",
        "Send message", "Assistant is typing",
        "Always answer in English. Be clear and concise, and prefer the available retail analytics tools for factual answers.",
        "Model discovery failed:", "Sorry, I couldn't complete that request:", "Stop generating", "Retry", "No models available",
        [
            ("What is C003's total spend and segment?", "What is customer C003 total spend and which segment are they in?"),
            ("Which segment has the lowest retention?", "Which customer segment has the lowest retention rate?"),
            ("Show C003's recent purchases", "Show the recent purchases and product categories for customer C003."),
            ("Compare segment retention rates", "Compare the retention rates of all customer segments."),
            ("List three retail KPIs", "List three important retail KPIs, one per line.")
        ]);
}
