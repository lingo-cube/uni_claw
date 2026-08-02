using System.Text.Json;

namespace UniClaw.Host.Analysis;

/// <summary>
/// BaselineProfile — loads a scenario's append-only baseline file
/// (<c>artifacts/baselines/&lt;scenarioId&gt;.jsonl</c>) and computes p50/p95 percentiles of
/// itemsVisited, stepsUsed, and aiLatency over the historical runs (trace-span-observability
/// D6: thresholds are derived from data only once ≥ 10 records exist).
/// With fewer than 10 records <see cref="IsReady"/> is false and every percentile is 0 —
/// callers must operate in cold-start mode (only Halt and Warn fire).
/// Corrupt lines are skipped with a logged warning; the remaining lines still count.
/// </summary>
public sealed class BaselineProfile
{
    private const int MinimumRecords = 10;

    /// <summary>Scenario whose baseline file this profile summarizes.</summary>
    public string ScenarioId { get; }

    /// <summary>Number of valid aggregate records loaded from the baseline file.</summary>
    public int RecordCount { get; }

    /// <summary>True once the baseline holds 10 or more records; percentiles are only valid then.</summary>
    public bool IsReady => RecordCount >= MinimumRecords;

    /// <summary>p50 of the historical itemsVisited distribution (0 when not IsReady).</summary>
    public double ItemsVisitedP50 { get; }

    /// <summary>p95 of the historical itemsVisited distribution (0 when not IsReady).</summary>
    public double ItemsVisitedP95 { get; }

    /// <summary>p50 of the historical stepsUsed distribution (0 when not IsReady).</summary>
    public double StepsUsedP50 { get; }

    /// <summary>p95 of the historical stepsUsed distribution (0 when not IsReady).</summary>
    public double StepsUsedP95 { get; }

    /// <summary>p50 of the historical aiLatency distribution (0 when not IsReady).</summary>
    public double AiLatencyP50 { get; }

    /// <summary>p95 of the historical aiLatency distribution (0 when not IsReady).</summary>
    public double AiLatencyP95 { get; }

    private BaselineProfile(
        string scenarioId,
        int recordCount,
        double itemsVisitedP50,
        double itemsVisitedP95,
        double stepsUsedP50,
        double stepsUsedP95,
        double aiLatencyP50,
        double aiLatencyP95)
    {
        ScenarioId = scenarioId;
        RecordCount = recordCount;
        ItemsVisitedP50 = itemsVisitedP50;
        ItemsVisitedP95 = itemsVisitedP95;
        StepsUsedP50 = stepsUsedP50;
        StepsUsedP95 = stepsUsedP95;
        AiLatencyP50 = aiLatencyP50;
        AiLatencyP95 = aiLatencyP95;
    }

    /// <summary>
    /// Load the scenario's baseline profile from <c>baselines/&lt;scenarioId&gt;.jsonl</c>.
    /// Returns null when the file does not exist. Corrupt or partial lines are skipped with
    /// a warning and do not count toward <see cref="RecordCount"/>.
    /// </summary>
    /// <param name="scenarioId">Scenario identifier (must be a single safe path segment).</param>
    /// <param name="artifactsRoot">Root of the artifacts directory (e.g. "artifacts").</param>
    public static BaselineProfile? Load(string scenarioId, string artifactsRoot = "artifacts")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        var path = Path.Combine(artifactsRoot, "baselines", $"{scenarioId}.jsonl");
        if (!File.Exists(path))
            return null;

        var itemsVisited = new List<double>();
        var stepsUsed = new List<double>();
        var aiLatency = new List<double>();

        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryParseRecord(line, out var record))
            {
                Console.Error.WriteLine(
                    $"[BaselineProfile] Skipping corrupt baseline line {lineNumber} " +
                    $"for scenario '{scenarioId}': not a valid aggregate record.");
                continue;
            }

            itemsVisited.Add(record.ItemsVisited);
            stepsUsed.Add(record.StepsUsed);
            aiLatency.Add(record.AiLatency);
        }

        var count = itemsVisited.Count;
        if (count < MinimumRecords)
        {
            // Cold-start: no threshold is derived from an insufficient sample.
            return new BaselineProfile(
                scenarioId,
                count,
                0, 0, 0, 0, 0, 0);
        }

        return new BaselineProfile(
            scenarioId,
            count,
            Percentile(itemsVisited, 0.50),
            Percentile(itemsVisited, 0.95),
            Percentile(stepsUsed, 0.50),
            Percentile(stepsUsed, 0.95),
            Percentile(aiLatency, 0.50),
            Percentile(aiLatency, 0.95));
    }

    /// <summary>
    /// A parsed aggregate record: the three fields percentile computation consumes.
    /// </summary>
    private readonly record struct Record(double ItemsVisited, double StepsUsed, double AiLatency);

    /// <summary>
    /// Parse one JSONL line into a Record. Returns false when the line is not a valid JSON
    /// object or any of the three percentile inputs (itemsVisited, stepsUsed, aiLatencyP50 —
    /// with aiLatencyP95 accepted as a fallback since both equal the single-run average) is
    /// missing or not numeric.
    /// </summary>
    private static bool TryParseRecord(string line, out Record record)
    {
        record = default;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryGetNumber(root, "itemsVisited", out var itemsVisited)
                || !TryGetNumber(root, "stepsUsed", out var stepsUsed))
            {
                return false;
            }

            if (!TryGetNumber(root, "aiLatencyP50", out var aiLatency)
                && !TryGetNumber(root, "aiLatencyP95", out aiLatency))
            {
                return false;
            }

            record = new Record(itemsVisited, stepsUsed, aiLatency);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Read a numeric JSON property by name (works for both int and double values).</summary>
    private static bool TryGetNumber(JsonElement root, string propertyName, out double value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var element)
            && element.TryGetDouble(out value);
    }

    /// <summary>
    /// p/q percentile over the values: p50 = element at floor(count * 0.50),
    /// p95 = element at floor(count * 0.95) of the sorted array.
    /// Only called with a non-empty sample (RecordCount ≥ 10).
    /// </summary>
    private static double Percentile(IReadOnlyList<double> values, double quantile)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        return sorted[(int)Math.Floor(sorted.Length * quantile)];
    }
}
