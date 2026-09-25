# AGT-002 implementation plan

## Slice A — protocol foundation

1. Add `UniClaw.Agent.Dsh` adapter assembly referencing the existing Kernel protocol
   records.
2. Add deterministic reflection-based schema generation, canonical JSON and SHA-256
   schema hash. Keep the generated artifact under `schemas/agt-002/` and expose the
   generator for verification.
3. Add protocol metadata, capability manifest, handshake validation and typed JSON-RPC
   envelopes.
4. Add fake and replay test realizations and roundtrip tests for all four `AgentDecision`
   cases.

## Slice B — runtime/decision channel

1. Define the shared decision-channel contract from the existing .NET protocol records.
2. Implement `DshOpenedDecisionChannel` as the only Product realization: DSH opens the
   physical channel; Kernel validates and registers the semantic attachment to an explicit
   Product Session/Primary Run; Kernel may revoke the attachment.
3. Keep one active `DecisionRequest` per Run, generation/request-id correlation, timeout,
   abort, late/stale/duplicate drop diagnostics, and fail-closed decision validation.
4. Move `StdioDecisionChannel`, fake, and replay realizations to tests/Harness. They must
   implement the same contract but must not enter the Product Host composition root or act
   as runtime fallback.
5. Add a DSH Product profile/plugin skeleton under `platforms/dsh/` with the same handshake
   and `submit_decision` allowlist; do not bind the formal path to port 3080.

## Coverage evidence

- Protocol tests cover malformed union payloads, unknown capability rejection,
  duplicate capability rejection, runtime generator parity for the checked-in
  JSON Schema and TypeScript artifacts, and replay of all four decision variants.
- Lifecycle tests cover stale generation response fail-closed behavior; the existing
  late and duplicate diagnostic scenarios remain in the same suite.
- One parameterized `DecisionChannelConformanceTests` suite runs against DSH-opened,
  stdio, fake, and replay realizations. Product composition tests assert that no stdio
  concrete type is reachable from the Product root.

## Review and verification

- Sol reviews every Luna-owned test/fixture/documentation change.
- Sol review result: PASS. Focused DSH suite 21/21 and full solution suite 781/781
  are green; only the NuGet vulnerability-cache permission warning remains.
- Run contract, deterministic, and scenario tests; record the four tuple in state.md.
- Only after Slice A/B and Sol review, run free/local model E2E, then DeepSeek Flash E2E.
  Both model E2E gates remain pending because this environment has no DSH executable
  or provider credential; the available free-model client failed before a response.

## Accepted architecture disposition — 2026-09-25

- DSH-backed UniAgent is the Product Realization; Product Runtime/Kernel remains the
  canonical Product authority.
- DSH opens the physical channel; Kernel owns semantic attachment/revocation.
- Kernel creates and pushes `DecisionRequest`; DSH returns `AgentDecision` through
  `submit_decision`.
- stdio/fake/replay are test realizations only. No Product fallback to stdio.
- The shared contract is tested by one parameterized conformance suite.
