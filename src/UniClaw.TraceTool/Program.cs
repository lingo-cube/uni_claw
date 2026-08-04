using System.CommandLine;
using System.CommandLine.Invocation;
using UniClaw.TraceTool.Commands;

namespace UniClaw.TraceTool;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Trace analysis tool for UniClaw runs")
        {
            Name = "trace",
        };

        rootCommand.Add(CreateListCommand());
        rootCommand.Add(CreateTimelineCommand());
        rootCommand.Add(CreateDiagnoseCommand());
        rootCommand.Add(CreateDiffCommand());
        rootCommand.Add(CreateReportCommand());
        rootCommand.Add(CreateInteractiveCommand());
        rootCommand.Add(CreateVerifyCommand());
        rootCommand.Add(CreateWatchCommand());

        rootCommand.SetHandler((InvocationContext context) =>
        {
            Console.WriteLine("trace - trace analysis tool. Run 'trace <subcommand> --help' for details.");
        });

        return rootCommand.InvokeAsync(args);
    }

    // ── Shared option factories ────────────────────────────────────
    // System.CommandLine beta4 has no AddGlobalOption and no Task<int> handler
    // overloads: each subcommand carries its own --format instance and handlers set
    // InvocationContext.ExitCode from the TraceExitCodes contract.

    private static Option<string> NewFormatOption() =>
        new Option<string>(
            "--format",
            getDefaultValue: () => "table",
            description: "Output format: table or json.")
            .FromAmong("table", "json");

    private static Option<string> NewRunOption() =>
        new("--run", "Run directory to analyze.");

    private static Command With(Command command, params Option[] options)
    {
        foreach (var option in options)
            command.AddOption(option);
        return command;
    }

    // ── list ────────────────────────────────────────────────────────

    private static Command CreateListCommand()
    {
        var dirOption = new Option<string>(
            "--dir",
            getDefaultValue: () => "artifacts/runs",
            description: "Directory to scan for run directories.");
        var statusOption = new Option<string>(
            "--status",
            "Filter runs by result status (e.g. failure, success).");
        var taskIdOption = new Option<string>(
            "--task-id",
            "Filter runs by task id.");
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 50,
            description: "Maximum number of runs to print.");
        var formatOption = NewFormatOption();

        var command = With(
            new Command("list", "Discover run directories."),
            dirOption,
            statusOption,
            taskIdOption,
            limitOption,
            formatOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await TraceCommands.ListAsync(
                context.ParseResult.GetValueForOption(dirOption),
                context.ParseResult.GetValueForOption(statusOption),
                context.ParseResult.GetValueForOption(taskIdOption),
                context.ParseResult.GetValueForOption(limitOption),
                context.ParseResult.GetValueForOption(formatOption) ?? "table");
        });
        return command;
    }

    // ── timeline ────────────────────────────────────────────────────

    private static Command CreateTimelineCommand()
    {
        var runOption = NewRunOption();
        var thresholdOption = new Option<int>(
            "--threshold",
            getDefaultValue: () => 0,
            description: "Highlight steps slower than this many milliseconds.");
        var formatOption = NewFormatOption();

        var command = With(
            new Command("timeline", "Performance timeline of a run."),
            runOption,
            thresholdOption,
            formatOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await TraceCommands.TimelineAsync(
                context.ParseResult.GetValueForOption(runOption),
                context.ParseResult.GetValueForOption(thresholdOption),
                context.ParseResult.GetValueForOption(formatOption) ?? "table");
        });
        return command;
    }

    // ── diagnose ────────────────────────────────────────────────────

    private static Command CreateDiagnoseCommand()
    {
        var runOption = NewRunOption();
        var formatOption = NewFormatOption();

        var command = With(
            new Command("diagnose", "Root-cause diagnosis of a run."),
            runOption,
            formatOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await TraceCommands.DiagnoseAsync(
                context.ParseResult.GetValueForOption(runOption),
                context.ParseResult.GetValueForOption(formatOption) ?? "table");
        });
        return command;
    }

    // ── diff ────────────────────────────────────────────────────────

    private static Command CreateDiffCommand()
    {
        var runAOption = new Option<string>("--run-a", "First run directory (A).");
        var runBOption = new Option<string>("--run-b", "Second run directory (B).");
        var formatOption = NewFormatOption();

        var command = With(
            new Command("diff", "Structured comparison across two runs."),
            runAOption,
            runBOption,
            formatOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await TraceCommands.DiffAsync(
                context.ParseResult.GetValueForOption(runAOption),
                context.ParseResult.GetValueForOption(runBOption),
                context.ParseResult.GetValueForOption(formatOption) ?? "table");
        });
        return command;
    }

    // ── report ──────────────────────────────────────────────────────

    private static Command CreateReportCommand()
    {
        var runOption = NewRunOption();
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "markdown",
            description: "Report format: markdown or json.")
            .FromAmong("markdown", "json");
        var outOption = new Option<string>(
            "--out",
            "Output file path (defaults to stdout).");

        var command = With(
            new Command("report", "Export a Markdown/Mermaid report for a run."),
            runOption,
            formatOption,
            outOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await TraceCommands.ReportAsync(
                context.ParseResult.GetValueForOption(runOption),
                context.ParseResult.GetValueForOption(formatOption) ?? "markdown",
                context.ParseResult.GetValueForOption(outOption));
        });
        return command;
    }

    // ── interactive ──────────────────────────────────────────────────

    private static Command CreateInteractiveCommand()
    {
        var runOption = NewRunOption();

        var command = With(
            new Command("interactive", "Terminal.Gui browser for a run."),
            runOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await InteractiveTui.RunAsync(
                context.ParseResult.GetValueForOption(runOption),
                CancellationToken.None);
        });
        return command;
    }

    // ── verify ──────────────────────────────────────────────────────

    private static Command CreateVerifyCommand()
    {
        var runOption = new Option<string?>("--run", "Single run directory to verify.");
        var dirOption = new Option<string?>("--dir", "Root directory for batch verify.");
        var statusOption = new Option<string?>("--status", () => "pending", "Filter runs by status.");
        var taskIdOption = new Option<string?>("--task-id", "Filter runs by task id.");
        var formatOption = NewFormatOption();

        var command = With(
            new Command("verify", "Verification rule engine over a run."),
            runOption,
            dirOption,
            statusOption,
            taskIdOption,
            formatOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await TraceCommands.VerifyAsync(
                context.ParseResult.GetValueForOption(runOption),
                context.ParseResult.GetValueForOption(dirOption),
                context.ParseResult.GetValueForOption(statusOption),
                context.ParseResult.GetValueForOption(taskIdOption),
                context.ParseResult.GetValueForOption(formatOption) ?? "table");
        });
        return command;
    }

    // ── watch ───────────────────────────────────────────────────────

    private static Command CreateWatchCommand()
    {
        var runIdOption = new Option<string>("--run-id", "Run ID to watch.");
        var dirOption = new Option<string>("--dir", "Root directory to scan for the run.");
        var intervalOption = new Option<int>(
            "--interval",
            getDefaultValue: () => 5000,
            description: "Poll interval in milliseconds.");

        var command = With(
            new Command("watch", "Poll a run-id until pending_verification then auto-verify."),
            runIdOption,
            dirOption,
            intervalOption);

        command.SetHandler(async (context) =>
        {
            context.ExitCode = await TraceCommands.WatchAsync(
                context.ParseResult.GetValueForOption(runIdOption) ?? "",
                context.ParseResult.GetValueForOption(dirOption) ?? "",
                context.ParseResult.GetValueForOption(intervalOption));
        });
        return command;
    }
}
