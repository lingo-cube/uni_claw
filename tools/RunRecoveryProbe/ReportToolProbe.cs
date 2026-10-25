using System.Text.Json;
using UniClaw.Host.Dsh;

internal static class ReportToolProbe
{
    internal static async Task Run(string mode, string directory)
    {
        var registry = Path.Combine(directory, "registry.yaml");
        var runsRoot = Path.Combine(directory, "runs");
        var host = RuntimeToolHost.Load(registry);
        var invoked = await host.InvokeRequiredReportAsync(runsRoot, "source", "source");
        await File.WriteAllTextAsync(Path.Combine(directory, "result-" + Environment.ProcessId + ".json"),
            JsonSerializer.Serialize(new { ProcessId = Environment.ProcessId, Mode = mode, invoked.Delivery.Record.DeliveryId,
                Status = invoked.Delivery.Record.Status.ToString(), invoked.Ok, Reason = invoked.Result.StderrTail }));
    }
}
