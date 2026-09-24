# AGT-002 implementation plan

## Slice A — protocol foundation

1. Add `UniClaw.Agent.Dsh` adapter assembly referencing the existing Kernel protocol
   records.
2. Add deterministic reflection-based schema generation, canonical JSON and SHA-256
   schema hash. Keep the generated artifact under `schemas/agt-002/` and expose the
   generator for verification.
3. Add protocol metadata, capability manifest, handshake validation and typed JSON-RPC
   envelopes.
4. Add fake and replay sidecars and roundtrip tests for all four `AgentDecision` cases.

## Slice B — runtime/transport

1. Add a transport interface and a stdio JSON-RPC implementation with a bounded turn.
2. Add the adapter lifecycle: one Product session per adapter, explicit DSH session id,
   one active consultation per Run, generation/request-id correlation, timeout and
   abort handling.
3. Add late/stale/duplicate drop diagnostics and decision correlation validation.
4. Add a minimal headless sidecar skeleton under `platforms/dsh/` with the same handshake
   and `submit_decision` allowlist.

## Coverage evidence

- Protocol tests cover malformed union payloads, unknown capability rejection,
  duplicate capability rejection, runtime generator parity for the checked-in
  JSON Schema and TypeScript artifacts, and replay of all four decision variants.
- Lifecycle tests cover stale generation response fail-closed behavior; the existing
  late and duplicate diagnostic scenarios remain in the same suite.

## Review and verification

- Sol reviews every Luna-owned test/fixture/documentation change.
- Sol review result: PASS. Focused DSH suite 21/21 and full solution suite 781/781
  are green; only the NuGet vulnerability-cache permission warning remains.
- Run contract, deterministic, and scenario tests; record the four tuple in state.md.
- Only after Slice A/B and Sol review, run free/local model E2E, then DeepSeek Flash E2E.
  Both model E2E gates remain pending because this environment has no DSH executable
  or provider credential; the available free-model client failed before a response.
