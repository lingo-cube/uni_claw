# Switch State Reading — L2 Contract Purchase

> Status: PROPOSED
> Baseline: 74ce42b (SwitchStateReader contract revision with PerceptionFrame)
> Module: Perception/Vision
> Capability: SwitchStateReading

## Summary

Purchase the L2 contract for reading visual toggle/switch ON/OFF/UNKNOWN state from an immutable perception frame.

This is the first Vision vertical slice in the Capability Module Baseline.

## Motivation

The graduated Runtime Core correctly handles `ObservedElement.SwitchState` (true/false/null) but has no mechanism to populate it from real perception. The `ISwitchStateReader` port provides this mechanism as a frame-scoped, stateless, evidence-producing contract that integrates without any Core changes.

## Contract

```csharp
public interface ISwitchStateReader
{
    PerceptionFrame Frame { get; }
    ValueTask<bool?> ReadAsync(ElementBounds bounds, CancellationToken ct = default);
}
```

- **true** = visually ON
- **false** = visually OFF
- **null** = UNKNOWN / insufficient evidence / invalid bounds / not a recognizable switch

## Frame Safety

Each reader is bound to one immutable `PerceptionFrame`. Stale-frame evidence MUST NOT enter a fresh Observation. The `SwitchStateValidation.ValidateFrameMatch` method enforces this invariant in production composition:

```csharp
bool? SwitchStateValidation.ValidateFrameMatch(
    ISwitchStateReader reader,
    PerceptionFrame currentFrame,
    bool? readResult);
```

Mismatch → fail closed (returns null).

## Integration

The reader fits into the existing flow without Core changes:

```
Fresh frame → create PerceptionFrameContext
  → create frame-bound ISwitchStateReader
  → for each toggle candidate: ReadAsync(bounds)
  → validate frame match
  → ObservedElement.SwitchState = result
  → Observation
  → existing Container/Agent pipeline
```

## Non-Goals

- Image classification implementation (deferred to adapter project)
- Provider framework / registry
- Vision facade
- StateClassifier
- VLM / LLM integration
