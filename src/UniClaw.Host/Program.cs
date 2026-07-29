using UniClaw.Host.Commands;

namespace UniClaw.Host;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            var application = new HostApplication(
                new HostCompositionFactory(),
                Console.Out,
                Console.Error);
            return await application.RunAsync(args, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
