using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.PhysicalHost;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Semantic.Android.Visual;
using UniClaw.Semantic.Settings;

namespace UniClaw.Settings.ValidationHost;

/// <summary>External scenario validation runner. It is not wired to DriverHost or run.start.</summary>
internal static class ValidationRunner
{
    public static async Task<int> RunAsync(
        string scenario,
        PhysicalHostOptions options,
        CancellationToken cancellationToken)
    {
        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, cancellationToken);
        if (!resolution.IsResolved)
            return 2;

        if (string.IsNullOrWhiteSpace(options.VisionSocketPath))
            throw new InvalidOperationException("Validation execution requires --vision-socket; production host owns Vision lifecycle.");

        var raw = PhysicalHostComposition.BuildRealEnvironment(
            options, resolution.Serial!, options.VisionSocketPath,
            new AdbUiHierarchySource(resolution.Serial!, options.AdbExecutable),
            new AndroidVisualControlStateReaderFactory());
        var semantic = new SemanticCapabilityRuntime(new SettingsSemanticCapability());
        var environment = new SemanticCapabilityEnvironment(raw, semantic);
        var attach = PhysicalHostComposition.CreateAttach(options, resolution.Serial!);
        var graph = PhysicalHostComposition.BuildRuntimeGraph(
            environment,
            options,
            attach,
            launchIntentAction: options.LaunchIntentAction);

        Console.WriteLine($"VALIDATION_COMPOSITION scenario={scenario} capability={nameof(SettingsSemanticCapability)}");
        Console.WriteLine("VALIDATION_GRAPH_READY");
        return 0;
    }
}
