using GitHub.Copilot;
using SdkLabs;

try
{
    var options = LabOptions.Parse(args);
    if (options.Command == "help")
    {
        Console.WriteLine(LabOptions.Help);
        return 0;
    }

    if (options.Command == "self-test")
    {
        SelfTests.Run();
        return 0;
    }

    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; timeout.Cancel(); };
    Console.CancelKeyPress += cancel;
    try
    {
        var cliPath = Environment.GetEnvironmentVariable("COPILOT_CLI_PATH");
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio(
                string.IsNullOrWhiteSpace(cliPath) ? "copilot" : cliPath),
            LogLevel = CopilotLogLevel.Error
        });
        await client.StartAsync(timeout.Token);

        if (options.DeleteId is not null)
        {
            await client.DeleteSessionAsync(options.DeleteId, timeout.Token);
            Console.WriteLine($"Deleted explicitly requested lab session: {options.DeleteId}");
            return 0;
        }

        var models = await client.ListModelsAsync(timeout.Token);
        var model = LabOptions.SelectModel(models.Select(m => m.Id), options.Model);
        Console.WriteLine($"Model: {model}");
        await Labs.RunAsync(client, options, model, timeout.Token);
        return 0;
    }
    finally
    {
        Console.CancelKeyPress -= cancel;
    }
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled or timed out. Check CLI authentication/network, or increase --timeout seconds.");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine($"SdkLabs: {error.Message}");
    Console.Error.WriteLine("Use --help for syntax. Set COPILOT_CLI_PATH to your Copilot CLI executable if needed.");
    return 1;
}
