using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Tests.Baseline;

/// <summary>
/// Static utility class for writing baseline test reports in JSON and Markdown formats.
/// </summary>
public static class BaselineReportWriter
{
    /// <summary>
    /// Writes a single baseline report as JSON.
    /// </summary>
    /// <param name="reportsDir">Directory to write the report</param>
    /// <param name="report">Report to serialize</param>
    public static void WriteJson(string reportsDir, BaselineReport report)
    {
        try
        {
            var options = new JsonSerializerOptions(DomainJsonOptions.Default)
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(report, options);
            var filename = $"{report.Scenario}.json";
            var path = Path.Combine(reportsDir, filename);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BaselineReport] JSON write failed for {report.Scenario}: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes an aggregated index.md file with all reports.
    /// </summary>
    /// <param name="reportsDir">Directory to write the index</param>
    /// <param name="reports">All reports to include in the index</param>
    public static void WriteIndex(string reportsDir, ImmutableArray<BaselineReport> reports)
    {
        try
        {
            var total = reports.Length;
            var passed = reports.Count(r => r.AllPassed);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            var lines = new List<string>
            {
                "# Baseline Test Report",
                "",
                $"> **Run**: {timestamp}",
                $"> **Pass Rate**: {passed}/{total} ({(total > 0 ? (passed * 100 / total) : 0)}%)",
                ""
            };

            lines.Add("| Scenario | Status | Steps | Pages | Actions | Scrolls | Details |");
            lines.Add("|----------|--------|-------|-------|---------|---------|---------|");

            foreach (var report in reports.OrderBy(r => r.Scenario))
            {
                var status = report.AllPassed ? "✅ PASS" : "❌ FAIL";
                var steps = report.ActualNumeric.TotalSteps;
                var pages = report.ActualNumeric.VisitedPagesCount;
                var actions = report.ActualNumeric.ActionHistoryCount;
                var scrolls = report.ActualNumeric.ScrollCount > 0
                    ? report.ActualNumeric.ScrollCount.ToString()
                    : "—";
                var details = string.Join(", ",
                    report.Details.Where(d => d.Passed).Select(d => d.RuleId.Split(':')[0]));

                lines.Add($"| {report.Scenario} | {status} | {steps} | {pages} | {actions} | {scrolls} | {details} |");
            }

            var path = Path.Combine(reportsDir, "index.md");
            File.WriteAllText(path, string.Join(Environment.NewLine, lines));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BaselineReport] Index write failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes all reports to the specified directory.
    /// </summary>
    /// <param name="reportsDir">Directory to write reports</param>
    /// <param name="reports">Reports to write</param>
    public static void WriteAll(string reportsDir, IEnumerable<BaselineReport> reports)
    {
        try
        {
            Directory.CreateDirectory(reportsDir);

            foreach (var report in reports)
            {
                WriteJson(reportsDir, report);
            }

            WriteIndex(reportsDir, reports.ToImmutableArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BaselineReport] WriteAll failed: {ex.Message}");
        }
    }
}
