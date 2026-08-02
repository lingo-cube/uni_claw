using UniClaw.Core.Observability;

namespace UniClaw.Host.Observability;

/// <summary>
/// Writes key traversal events to the console in real time, so operators
/// can follow engine progress without reading trace files.
/// </summary>
public static class ConsoleTrace
{
    public static void SessionStart(string traceId) =>
        Console.WriteLine($"🚀 [{Now}] session start  traceId={traceId}");

    public static void SessionEnd() =>
        Console.WriteLine($"🏁 [{Now}] session end");

    public static void Log(ExecutionRecord r)
    {
        if (string.IsNullOrEmpty(r.Action))
            return;

        var icon = r.Action switch
        {
            "step_start" => "👣",
            "step_end" => "  ",
            "page_analysis" => "🔍",
            not null when r.Action.StartsWith("safety.") => "⚡",
            not null when r.Action.StartsWith("scroll_") => "📜",
            not null when r.Action.StartsWith("verification_") => "🔬",
            _ => "·",
        };

        var detail = r.Status is { Length: > 0 } s ? $" → {s}" : "";
        Console.WriteLine($"{icon} [{Now}] step={r.Context?.StepNumber,3} {r.Action}{detail}");
    }

    public static void Log(StateTransition t)
    {
        if (t.FsmType != "GlobalFSM")
            return;
        var detail = string.IsNullOrEmpty(t.Reason) ? "" : $" ({t.Reason})";
        Console.WriteLine($"🔄 [{Now}] {t.FromState} → {t.ToState}{detail}");
    }

    public static void Log(ErrorRecord r) =>
        Console.WriteLine($"❌ [{Now}] ERROR  {r.ErrorType}: {r.ErrorMessage}");

    public static void Log(PageTransition t) =>
        Console.WriteLine($"📄 [{Now}] page  {t.FromPage} → {t.ToPage}  ({t.TransitionType})");

    public static void Log(AICallRecord r)
    {
        var latency = r.LatencyMs > 0 ? $"  {r.LatencyMs:F0}ms" : "";
        var tokens = r.Tokens > 0 ? $"  {r.Tokens} tok" : "";
        Console.WriteLine($"⏳ [{Now}] AI call  capability={r.Capability}{latency}{tokens}");
    }

    private static string Now => DateTimeOffset.UtcNow.ToString("HH:mm:ss");
}
