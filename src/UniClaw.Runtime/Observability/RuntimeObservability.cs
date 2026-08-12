using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace UniClaw.Runtime.Observability;

/// <summary>Stable layer attribution for observability spans.</summary>
public static class ObservabilityLayer
{
    public const string Orchestration = "ORCHESTRATION";
    public const string Agent = "AGENT";
    public const string Startup = "STARTUP";
    public const string World = "WORLD";
    public const string Container = "CONTAINER";
    public const string Traversal = "TRAVERSAL";
    public const string Recovery = "RECOVERY";
    public const string Environment = "ENVIRONMENT";
    public const string Capability = "CAPABILITY";
    public const string Harness = "HARNESS";
}

/// <summary>Stable component identifiers for observability spans.</summary>
public static class ObservabilityComponent
{
    public const string RuntimeInvocation = "runtime.invocation";
    public const string AgentExecution = "agent.execution";
    public const string IntentExecution = "intent.execution";
    public const string ContainerRefresh = "container.refresh";
    public const string TraversalExecution = "traversal.execution";
    public const string EnvironmentObserve = "environment.observe";
    public const string EnvironmentExecute = "environment.execute";
    public const string RecoveryAttempt = "recovery.attempt";
    public const string CapabilityInvocation = "capability.invocation";
}

/// <summary>Structural observability outcomes — NOT semantic success/completion.</summary>
public static class ObservabilityOutcome
{
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string Unknown = "UNKNOWN";
}

/// <summary>
/// Bounded Runtime observability seam. Emits hierarchical BCL Activity spans
/// at approved Runtime boundaries. Owns NO per-run buffers, NO Harness types,
/// and NO persistence. No-throw: listener failures never escape into Runtime.
/// </summary>
public static class RuntimeObservability
{
    /// <summary>Stable source identity — never derived from CLR type names.</summary>
    public const string SourceName = "UniClaw.Runtime";

    private static readonly ActivitySource Source = new(SourceName, "1.0.0");

    /// <summary>Start an observability span at an approved boundary.</summary>
    public static Activity? StartSpan(
        string name,
        string layer,
        string component,
        Activity? parent = null)
    {
        try
        {
            var parentId = parent?.Id ?? Activity.Current?.Id;
            var activity = parentId is not null
                ? Source.StartActivity(name, ActivityKind.Internal, parentId)
                : Source.StartActivity(name, ActivityKind.Internal);

            if (activity is null) return null;

            activity.SetTag("layer", layer);
            activity.SetTag("component", component);
            return activity;
        }
        catch
        {
            // Fail-open: listener errors never escape into Runtime
            return null;
        }
    }

    /// <summary>Set a structured attribute on an observability span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTag(Activity? activity, string key, string? value)
    {
        try { activity?.SetTag(key, value); }
        catch { /* fail-open */ }
    }

    /// <summary>Add a point event to an observability span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddEvent(Activity? activity, string eventName)
    {
        try { activity?.AddEvent(new ActivityEvent(eventName)); }
        catch { /* fail-open */ }
    }

    /// <summary>Mark the span as completed with an observability outcome.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Complete(Activity? activity, string outcome)
    {
        if (activity is null) return;
        try
        {
            activity.SetTag("outcome", outcome);
            activity.Stop();
        }
        catch { /* fail-open */ }
    }

    /// <summary>Mark the span as completed with exception information (fail-open).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CompleteWithException(Activity? activity, Exception? ex)
    {
        if (activity is null) return;
        try
        {
            if (ex is OperationCanceledException)
                activity.SetTag("outcome", ObservabilityOutcome.Cancelled);
            else
                activity.SetTag("outcome", ObservabilityOutcome.Failed);
            activity.SetTag("error", ex?.GetType().Name);
            activity.Stop();
        }
        catch { /* fail-open */ }
    }

    /// <summary>Wrap execution of a span at an approved boundary. Fail-open.</summary>
    public static async Task<T> TraceAsync<T>(
        string name,
        string layer,
        string component,
        Func<Activity?, Task<T>> execute)
    {
        var activity = StartSpan(name, layer, component);
        try
        {
            var result = await execute(activity).ConfigureAwait(false);
            Complete(activity, ObservabilityOutcome.Succeeded);
            return result;
        }
        catch (OperationCanceledException)
        {
            Complete(activity, ObservabilityOutcome.Cancelled);
            throw;
        }
        catch (Exception)
        {
            Complete(activity, ObservabilityOutcome.Failed);
            throw;
        }
    }

    /// <summary>Wrap execution of a span. Fail-open. Non-async variant.</summary>
    public static T Trace<T>(
        string name,
        string layer,
        string component,
        Func<Activity?, T> execute)
    {
        var activity = StartSpan(name, layer, component);
        try
        {
            var result = execute(activity);
            Complete(activity, ObservabilityOutcome.Succeeded);
            return result;
        }
        catch (OperationCanceledException)
        {
            Complete(activity, ObservabilityOutcome.Cancelled);
            throw;
        }
        catch (Exception)
        {
            Complete(activity, ObservabilityOutcome.Failed);
            throw;
        }
    }
}
