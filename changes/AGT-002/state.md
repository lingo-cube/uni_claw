# AGT-002 — DeepSeek Harness Realization

lifecycle_state: verified · disposition: none · depth: decision-heavy · base: 82ff9362

## Status

VERIFIED — Slice A protocol and Q21–Q23 decision-channel migration are complete;
F1–F4 review fixes are verified; the formal Product transport is DSH-opened
channel with Kernel semantic attachment.
Luna mechanical completion was reviewed by Sol.
Model E2E remains blocked by the local environment: no DSH executable or
`DEEPSEEK_API_KEY` is available, and the free-model smoke attempt through
`opencode` failed before a model response (`Unexpected server error`).
AGT-001 and RUN-005 remain closed and unchanged.

## Decisions

- 2026-09-25 · `.NET` existing Kernel protocol records remain the Product protocol
  authority; the DSH project is an adapter/transport consumer, not a second protocol owner.
- 2026-09-25 · DSH-backed UniAgent is the Product Realization; Product Runtime/Kernel
  remains canonical authority. DSH opens the physical decision channel; Kernel registers
  and may revoke the semantic attachment.
- 2026-09-25 · stdio/fake/replay channels are test/Harness realizations only. They implement
  the same decision-channel contract and are not reachable from the Product composition root;
  no runtime fallback to stdio is allowed.
- 2026-09-25 · Kernel creates and pushes `DecisionRequest`; DSH returns `AgentDecision` via
  `submit_decision`. Q1–Q23 architecture grill reached shared understanding.

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: |
    dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --no-restore --filter "FullyQualifiedName~DecisionChannelConformanceTests|FullyQualifiedName~AdapterLifecycleTests|FullyQualifiedName~DecisionChannelClosureTests"
    dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --no-restore
    dotnet test UniClaw.Kernel.slnx --no-restore
  expected: |
    focused F1–F4 suite: 71 passed, 0 failed
    Agent.Dsh suite: 87 passed, 0 failed
    full solution: 847 passed, 0 failed
    only the existing NU1900 vulnerability-cache permission warning remains
  actual: |
    focused F1–F4 suite: 71 passed, 0 failed
    Agent.Dsh suite: 87 passed, 0 failed
    full solution: 847 passed, 0 failed
    only the existing NU1900 vulnerability-cache permission warning remains
  evidence: |
    Command output from all three commands above reports zero failed tests and the
    stated pass counts. F1–F4 regressions are covered by ProtocolFoundationTests.cs,
    AdapterLifecycleTests.cs, DecisionChannelConformanceTests.cs, and
    DecisionChannelClosureTests.cs; the full solution command also covers the
    remaining test projects.
```

## Status log

- 2026-09-25 · UNDERSTAND · AGT-001/RUN-005/working tree audited; no upstream
  conflict found; implementation scope persisted here.
- 2026-09-25 · PLAN · Keep the Product DSH-opened seam and apply only local F1–F4 fixes.
- 2026-09-25 · VERIFY · Added malformed decision payload, unknown capability,
  duplicate capability rejection, stale-generation, and generated artifact
  consistency coverage; DSH tests 17/17 passed.
- 2026-09-25 · REVIEW · Sol reviewed Luna-owned tests, fixtures, generated
  artifacts, and documentation; PASS. Sol then tightened duplicate capability
  rejection, timeout abort dispatch, strict nested union payload validation,
  generated SubmitDecision/Envelope declarations, and replay coverage; focused
  DSH tests 21/21 and full solution tests 781/781 passed. No upstream conflict.
- 2026-09-25 · VERIFY · Free/local model smoke was attempted through the
  available `opencode` client but failed before model output; DeepSeek Flash
  E2E is not runnable because no DSH executable or provider credential is present.

- 2026-09-25 · DISPOSITION · Q1–Q23 frozen: DSH-backed UniAgent Product Realization,
  DSH-opened physical channel, Kernel semantic attachment/revocation, test-only stdio,
  shared parameterized conformance suite, and no runtime fallback.
- 2026-09-25 · IMPLEMENT · Q21–Q23 migrated: `IDecisionChannel` and
  `DshOpenedDecisionChannel` are the Product seam/path; stdio/fake/replay moved to test
  fixtures; closure and parameterized conformance tests added.
- 2026-09-25 · VERIFY · Focused Agent.Dsh suite 66/66 and full solution 826/826 passed;
  only the existing NU1900 vulnerability-cache permission warning remains.
- 2026-09-25 · IMPLEMENT · F1–F3 review fixes are in place: linked-turn
  cancellation and awaited channel abort, per-turn abort state/barrier, single-flight
  attach with fenced retries, and attachment-generation invalidation for in-flight
  responses. Added deterministic cancellation, external-abort, retry, revoke, and
  late-response regressions.
- 2026-09-25 · IMPLEMENT · F4 guard now evaluates the Agent.Dsh MSBuild project
  graph and linked Compile/content/resource inputs, scans copied Product output
  TypeDefs/TypeRefs/assembly references, and enforces the sole
  `DshOpenedDecisionChannel` implementer, zero concrete peer implementations, and
  no dynamic registration/config fallback markers.
- 2026-09-25 · REVIEW · Standards and spec review disposition: PASS after the
  deterministic gate, adapter attachment serialization/epoch, mapping fence,
  evaluated project-input, and copied-DLL TypeDef findings were addressed; no
  upstream architecture or Product transport fallback was added.
- 2026-09-25 · VERIFY · Focused F1–F4 suite 71/71, full Agent.Dsh suite 87/87,
  and full solution 847/847 passed; only the existing NU1900 cache permission
  warning remains.
