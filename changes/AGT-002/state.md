# AGT-002 — DeepSeek Harness Realization

lifecycle_state: verified (E2E slice: authenticated bridge + real-model evidence) · disposition: none · depth: decision-heavy · base: 534d0aef

## Status

VERIFIED — F1–F4 review fixes at `534d0aef` are closed and committed
(`76995514`): handshake identity fields are mandatory protocol fields (missing
→ malformed handshake, fail closed, refuse attachment — negative coverage for
profileId/profileVersion/capabilityManifestHash); runtime configuration has a
single source (`.dsh/profiles/uniagent-prod.yaml` via the new
`UniagentProdYaml` loader — provider/model/baseUrl changes need no Product
recompile; the hardcoded model catalog and LocalWeb constants are deleted);
observer authority wording distinguishes canonical owner records / RuntimeOutcome
(Product truth) from Product Trace and DSH trajectory (diagnostic projections
the observer merely displays together); `ObserverEvent` carries
DecisionId/Generation/PolicyId correlation so decision-N timeout, decision-N+1
start, and decision-N late response are unambiguous in one timeline.

E2E: the authenticated 3080 DSH bridge is implemented and verified against a
real DSH instance. `@uniclaw/dsh-decision-channel` (repo `dsh/uniclaw-decision-channel/`)
registers the frozen single-tool `submit_decision` capability and the three
authenticated Product routes on the /api lane; `DshOpenedHttpPeer` is the sole
sanctioned concrete `IDshOpenedChannelPeer` (closure-guarded). Transport auth
is the DSH browser-session cookie minted from the harness-home credential store
(`DshWebCredential`, known-answer tested). Live evidence
(`evidence/agt-002-real-model-e2e.md`): frozen-stamp handshake PASS on a real
DSH session; full-stack real-model Act capture through the Product adapter
(5.8 s, DecisionId echoed); §11-shaped Policy decision captured intact
(match 24 / termination 20 / bounded applications / toggle-tap template,
21.7 s); deepseek-flash config-only switch → MODEL_CAPABILITY_FAIL
(no-submit prose answers; transport/schema PASS — no protocol accommodation);
turn-deadline failure E2E fail-closed with zero invented decisions. The
owner's live 3080 instance loads the plugin on its next restart (patch row
already wired). AGT-001/RUN-005 unchanged; no stdio fallback anywhere.

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
- 2026-09-25 · IMPLEMENT/VERIFY (review-fix + E2E slice, base `534d0aef`) ·
  F1–F4 closed (commit `76995514`; Agent.Dsh 98/98, full solution 858/858).
  Discovery mapped the real DSH web API (cookie-only auth; signed-cookie mint
  from the harness-home credential store; `POST /api/<endpoint>` envelope;
  plugin seam via `ctx.connection.fetch.register`). Implemented
  `@uniclaw/dsh-decision-channel` (zero-dep profile plugin: submit_decision
  tool + three authenticated routes; model-output normalization is defensive
  shape-mapping only — semantic validation stays fail-closed) and
  `DshOpenedHttpPeer` (sole concrete peer; closure guard updated to exactly
  one implementer; 401/403 → fail-closed auth error). Live E2E against a
  second real `dsh web` instance (port 3081, same web profile): frozen-stamp
  handshake accepted; full-stack real-model Act captured through the Product
  adapter; §11-shaped Policy captured intact; deepseek-flash →
  MODEL_CAPABILITY_FAIL (transport PASS); turn-deadline fail-closed E2E.
  Evidence: `evidence/agt-002-real-model-e2e.md`. Agent.Dsh 114/114
  (E2E env-gated: `UNICLAW_DSH_E2E_BASE`); full solution 874/874.
