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
5. Keep the deterministic stdio JSON-RPC sidecar only under
   `tests/UniClaw.Agent.Dsh.Tests/Fixtures/`; the formal Product path is the
   DSH-opened channel and is not bound to port 3080.

## Slice D — profile/model and Observer bring-up

1. Freeze `uniagent-prod` profile identity and capability manifest hash in the
   startup handshake; keep provider/model selection in independent configuration.
2. Use the running DSH service at `127.0.0.1:3080` as the external realization
   host and select the OpenCode model as `opencode-go/space-bunny-free` or
   `opencode-go/deepseek-flash`; the selected model must remain outside Product
   protocol and Kernel semantics.
3. Build a read-only `uniclaw-observer` projection over Product Trace,
   `AgentDecision` records, DSH trajectory, and diagnostics. It may correlate a
   timeline but cannot mutate Product, DSH, Policy, or Effect state.
4. Add deterministic human-readable evidence for Act, Policy, NoAction, Defer,
   and abort/timeout/late-response behavior. Keep real provider E2E evidence
   environment-gated.

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
- Sol review result: PASS. Focused DSH suite 66/66 and full solution suite 826/826
  are green; only the NuGet vulnerability-cache permission warning remains.
- Run contract, deterministic, and scenario tests; record the four tuple in state.md.
- Only after Slice A/B and Sol review, run free/local model E2E, then DeepSeek Flash E2E.
  Both model E2E gates remain pending until the authenticated `127.0.0.1:3080`
  service path is wired to the Product decision channel and a model response is
  captured.
- Slice D is locally verified through the Agent.Dsh suite and full solution suite;
  external model E2E remains blocked until the authenticated service path produces
  Product `submit_decision` evidence.

## Accepted architecture disposition — 2026-09-25

- DSH-backed UniAgent is the Product Realization; Product Runtime/Kernel remains the
  canonical Product authority.
- DSH opens the physical channel; Kernel owns semantic attachment/revocation.
- Kernel creates and pushes `DecisionRequest`; DSH returns `AgentDecision` through
  `submit_decision`.
- stdio/fake/replay are test realizations only. No Product fallback to stdio.
- The shared contract is tested by one parameterized conformance suite.
