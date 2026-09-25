# AGT-002 — DeepSeek Harness Realization

lifecycle_state: implementing · review_state: passed · disposition: accepted · depth: decision-heavy · base: 04dd6457

## Status

IMPLEMENTING — Slice A protocol and the test-side transport realization are complete;
the formal Product transport disposition is now DSH-opened channel with Kernel semantic
attachment. Luna mechanical completion was reviewed by Sol.
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
  method: dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --no-restore
  expected: malformed payloads, unknown capability, duplicate normalization, stale generation and generated artifacts are covered
  actual: 21 passed, 0 failed; schema JSON and d.ts match ProductProtocolSchemaGenerator output
  evidence: tests/UniClaw.Agent.Dsh.Tests/ProtocolFoundationTests.cs; tests/UniClaw.Agent.Dsh.Tests/AdapterLifecycleTests.cs
```

## Status log

- 2026-09-25 · UNDERSTAND→PLAN · AGT-001/RUN-005/working tree audited; no upstream
  conflict found; implementation scope persisted here.
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
