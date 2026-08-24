using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Observes the unchanged IEnvironment boundary without owning Runtime behavior.</summary>
public sealed class CapturingEnvironment : IEnvironment
{
    private readonly IEnvironment _inner;
    private readonly TraceCaptureSession _session;
    private readonly Func<Observation, string?>? _frameId;

    public CapturingEnvironment(IEnvironment inner, TraceCaptureSession session, Func<Observation, string?>? frameId = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _frameId = frameId;
    }

    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        Observation observation;
        try { observation = await _inner.ObserveAsync(cancellationToken); }
        catch (Exception ex) { Latch($"Environment observation failed: {ex.GetType().Name}"); throw; }

        try { _session.RecordObservation(observation, _frameId?.Invoke(observation)); }
        catch (Exception ex) { Latch($"Observation capture failed: {ex.GetType().Name}"); }
        return observation;
    }

    public async Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
    {
        try { _session.RecordDispatch(action); }
        catch (Exception ex) { Latch($"Dispatch capture failed: {ex.GetType().Name}"); }

        ActionResult result;
        try { result = await _inner.ExecuteAsync(action, cancellationToken); }
        catch (Exception ex) { Latch($"Environment execution failed: {ex.GetType().Name}"); throw; }

        try { _session.RecordResult(result); }
        catch (Exception ex) { Latch($"Action-result capture failed: {ex.GetType().Name}"); }
        return result;
    }

    private void Latch(string diagnostic)
    {
        try { _session.RecordFault(diagnostic); }
        catch { /* capture must never perturb Runtime behavior */ }
    }
}
