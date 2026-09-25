# AGT-002 — DeepSeek Harness Realization

lifecycle_state: verified · disposition: none · depth: decision-heavy · base: 82ff9362

## Status

VERIFIED — Slice A protocol and Q21–Q23 decision-channel migration are complete;
F1–F5 review fixes are verified; the formal Product transport is DSH-opened
channel with Kernel semantic attachment. F5 freezes Attach, AbortCurrentTurn, and
Revoke as bounded, cancelable, and fail-closed realization controls.
Luna mechanical completion was reviewed by Sol.
Model E2E is not yet closed. The local DSH web service is listening on
`127.0.0.1:3080` and returns its expected authentication challenge; the existing
Product decision-channel adapter is not yet wired to that service's authenticated
API. OpenCode exposes the selectable `opencode-go/space-bunny-free` and
`opencode-go/deepseek-flash` model identities for the bring-up. Shell-level
OpenCode credentials are not present, so no real model response was produced by
the previous command-line smoke attempt.
AGT-001 and RUN-005 remain closed and unchanged.

The AGT-002 bring-up slice now also contains the `uniagent-prod` profile/model
separation and a read-only `uniclaw-observer` projection. Observer data is a
timeline over Product Trace plus non-authoritative DSH trajectory/diagnostic
evidence; it has no Product command or DSH mutation surface and remains
disconnect-safe. The minimal human-facing surface is `.dsh/observer/index.html`;
its only input is an injected projection snapshot. Human-readable
Act/Policy/NoAction/Defer and abort/late-response demo evidence is recorded in
`evidence/agt-002-observer-demo.md`.

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
- 2026-09-25 · F5 · Attach owns a shared internal timeout/cancellation source while
  waiter cancellation remains local; adapter revoke fences the attachment epoch before
  bounded physical cleanup; abort acknowledgement is awaited through a realization-level
  deadline and late work is quarantined.

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: |
    dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --no-restore --filter "FullyQualifiedName~DecisionChannelConformanceTests|FullyQualifiedName~AdapterLifecycleTests|FullyQualifiedName~DecisionChannelClosureTests"
    dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --no-restore
    dotnet test UniClaw.Kernel.slnx --no-restore
  expected: |
    focused F1–F5 suite: 74 passed, 0 failed
    Agent.Dsh suite: 94 passed, 0 failed
    full solution: 854 passed, 0 failed
    only the existing NU1900 vulnerability-cache permission warning remains
  actual: |
    focused F1–F5 suite: 74 passed, 0 failed
    Agent.Dsh suite: 94 passed, 0 failed
    full solution: 854 passed, 0 failed
    only the existing NU1900 vulnerability-cache permission warning remains
  evidence: |
    Command output from all three commands above reports zero failed tests and the
    stated pass counts. F1–F5 regressions are covered by ProtocolFoundationTests.cs,
    AdapterLifecycleTests.cs, DecisionChannelConformanceTests.cs, and
    DecisionChannelClosureTests.cs; Slice D profile/Observer coverage is in
    ObserverProjectionTests.cs; the full solution command also covers the
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
- 2026-09-25 · VERIFY · The DSH web service was confirmed listening on
  `127.0.0.1:3080`; unauthenticated HTTP returns the service's expected 401
  challenge. This is a service/UI endpoint, not yet evidence of the Product
  `submit_decision` channel.

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
- 2026-09-25 · IMPLEMENT · F5 bounded control-plane realization: physical attach
  now uses a real shared CTS with timeout and fenced late completion; adapter attach
  no longer holds a gate across handshake, so semantic revoke proceeds immediately;
  abort acknowledgement and revoke cleanup are bounded with diagnostics and late
  operation observation. Added deterministic A1–A5 coverage.
- 2026-09-25 · VERIFY · Focused F1–F5 suite 74/74, full Agent.Dsh suite 90/90,
  and full solution 850/850 passed; only the existing NU1900 cache permission
  warning remains. AGT-002 F5 is complete with no upstream design conflict.
- 2026-09-25 · IMPLEMENT · Added `uniagent-prod` profile metadata, capability
  manifest hash in the startup handshake, independent model configuration, and
  read-only Observer projection/workspace. Existing stdio fixture was updated to
  report the frozen profile metadata; no Product fallback or authority surface
  was added.
- 2026-09-25 · REVIEW · Slice D reviewed against spec and repo boundaries: profileId,
  profileVersion and capabilityManifestHash are enforced in `ProductHandshake.Validate`;
  model selection stays a separate configuration record; `UnifiedObserverProjection`/
  `ObserverWorkspace` and the static observer page expose read-only projections with no
  command, DSH mutator or Product lifecycle method; the DSH service endpoint is
  configuration only and the Product runtime path is still the DSH-opened channel with
  no stdio/fake/replay fallback. PASS; no upstream architecture conflict.
- 2026-09-25 · VERIFY · Agent.Dsh suite 94/94 and full solution suite 854/854
  passed; only the existing NU1900 vulnerability-cache permission warning remains.
  Real provider/model E2E remains `E2E BLOCKED` pending authenticated
  Product-channel wiring and model response evidence.
