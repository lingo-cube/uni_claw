using Xunit;

namespace UniClaw.Runtime.Tests;

/// <summary>
/// Serializes test classes that hold a live <c>RuntimeTraceRecorder</c> (or drive
/// the RunExecutionCoordinator, which creates recorders per accepted run).
///
/// WHY: the BCL ActivityListener is process-global. A recorder claims its run's
/// capture scope from the first observed activity, so two classes emitting
/// <c>UniClaw.Runtime</c> activities in parallel can pre-claim each other's scope
/// (foreign-trace skip). Serializing the emitter classes keeps the run-scoped
/// capture tests deterministic; non-listener classes never create activities and
/// are unaffected.
/// </summary>
[CollectionDefinition("ObservabilityTraceEmitters", DisableParallelization = true)]
public sealed class ObservabilityTraceEmitterCollection
{
}