namespace UniClaw.Runtime.PhysicalHost;

/// <summary>Generic physical runtime host entry point.</summary>
public static class Program
{
    /// <summary>Starts the generic physical DriverHost when --serve is supplied.</summary>
    public static async Task<int> Main(string[] args)
    {
        PhysicalHostOptions options;
        try { options = PhysicalHostOptions.Parse(args); }
        catch (FormatException exception)
        {
            Console.Error.WriteLine($"ARGUMENT_ERROR {exception.Message}");
            return 64;
        }
        if (!options.Serve)
        {
            Console.Error.WriteLine("Production host exposes --serve; scenario validation belongs to an external validation host.");
            return 64;
        }
        using var cancellation = options.TimeoutSeconds is int seconds
            ? new CancellationTokenSource(TimeSpan.FromSeconds(seconds))
            : new CancellationTokenSource();
        using var server = PhysicalHostComposition.BuildDriverHostServer(options);
        server.Start();
        Console.WriteLine($"SERVING port={server.BoundPort}");
        try { await Task.Delay(Timeout.Infinite, cancellation.Token); }
        catch (OperationCanceledException) { }
        return 0;
    }
}
