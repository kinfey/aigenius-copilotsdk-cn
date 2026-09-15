namespace SdkLabs;

internal sealed record LabOptions(
    string Command, string? Model = null, string? Prompt = null,
    string? ResumeId = null, string? DeleteId = null, int TimeoutSeconds = 120,
    bool ExpectNoTools = false)
{
    public const string Help = """
        Original .NET examples for labs 03–06 (GitHub.Copilot.SDK 1.0.9)

        dotnet run --project src/AgentOrchestrator/samples/SdkLabs -- <command> [options]

        tools         Lab 03: customer purchases + category-summary tool
        events        Lab 04: streaming, first-token timing, usage and idle events
        permissions   Lab 04: allow/deny only in-memory demo tools; real callback counts
        sessions      Lab 05: create, dispose and resume a persistent lab session
        mcp           Lab 06: call the Microsoft Learn HTTP MCP server
        self-test     Offline checks of fixtures, safety policy and CLI parsing (no AI)

        --model ID          Validate ID against ListModelsAsync; default prefers
                            claude-haiku-4.5, otherwise the first available model.
        --prompt TEXT       Custom prompt for tools, events or mcp only.
        --expect-no-tools   tools only: require a successful reply with zero tool calls.
                            Without this flag, a customer tool must complete successfully.
        --timeout SECONDS   Overall timeout, 1–600 seconds (default 120).
        sessions --resume sdklabs-ID   Resume this lab session across processes.
        sessions --delete sdklabs-ID   Delete ONLY the explicitly named lab session.
        --help              Show help without starting the CLI or making AI requests.

        CLI: COPILOT_CLI_PATH overrides the 'copilot' executable found on PATH.
        Authenticate the CLI first. AI commands can consume Copilot quota.
        Sessions persist until explicitly deleted. No personal-session enumeration.
        """;

    public static LabOptions Parse(string[] args)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"])
            return new("help");
        if (args[0] is not ("tools" or "events" or "permissions" or "sessions" or "mcp" or "self-test"))
            throw new ArgumentException($"Unknown command '{args[0]}'.");
        var result = new LabOptions(args[0]);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index++)
        {
            var name = args[index];
            if (name is "--help" or "-h") return new("help");
            if (!seen.Add(name)) throw new ArgumentException($"Duplicate option '{name}'.");
            if (name == "--expect-no-tools")
            {
                result = result with { ExpectNoTools = true };
                continue;
            }
            if (++index == args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--"))
                throw new ArgumentException($"Missing value for '{name}'.");
            var value = args[index];
            result = name switch
            {
                "--model" => result with { Model = value },
                "--prompt" => result with { Prompt = value },
                "--resume" => result with { ResumeId = ValidateSessionId(value) },
                "--delete" => result with { DeleteId = ValidateSessionId(value) },
                "--timeout" when int.TryParse(value, out var seconds) && seconds is >= 1 and <= 600
                    => result with { TimeoutSeconds = seconds },
                _ => throw new ArgumentException($"Unknown option or invalid value: {name} {value}")
            };
        }
        if (result.Prompt is not null && result.Command is not ("tools" or "events" or "mcp"))
            throw new ArgumentException("--prompt is supported only for tools, events and mcp.");
        if (result.ExpectNoTools && result.Command != "tools")
            throw new ArgumentException("--expect-no-tools requires the tools command.");
        if ((result.ResumeId is not null || result.DeleteId is not null) && result.Command != "sessions")
            throw new ArgumentException("--resume and --delete require the sessions command.");
        if (result.ResumeId is not null && result.DeleteId is not null)
            throw new ArgumentException("Use --resume OR --delete, not both.");
        if (result.DeleteId is not null && result.Model is not null)
            throw new ArgumentException("--delete does not use a model.");
        if (result.Command == "self-test" && args.Length != 1)
            throw new ArgumentException("self-test takes no options.");
        return result;
    }

    public static string ValidateSessionId(string id)
    {
        if (!id.StartsWith("sdklabs-", StringComparison.Ordinal) || id.Length is <= 8 or > 100 ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Only explicit sdklabs-* session IDs containing ASCII letters, digits and hyphens are allowed.");
        return id;
    }

    public static string SelectModel(IEnumerable<string> availableModels, string? requested)
    {
        var models = availableModels.ToArray();
        if (models.Length == 0) throw new InvalidOperationException("ListModelsAsync returned no models. Check Copilot access.");
        if (requested is not null)
        {
            if (!models.Contains(requested, StringComparer.Ordinal))
                throw new ArgumentException($"Model '{requested}' is unavailable. Available IDs: {string.Join(", ", models)}");
            return requested;
        }
        return models.FirstOrDefault(m => m == "claude-haiku-4.5") ?? models[0];
    }
}
