# PER-018 binding / live-provider audit

Date: 2026-09-28 (Asia/Shanghai)

## Correct evidence surface

The earlier `opencode models opencode` result described the standalone OpenCode CLI catalogue. It did not inspect the DeepSeek Harness instance on port 3080 and is not evidence for the DSH provider.

The authoritative runtime binding for this task is DSH's `opencode-go` provider. DSH resolves its provider credential from its own credential store/environment reference; the API key is not copied into UniClaw configuration or Product code.

## Requested bindings

| logical profile | DSH provider | model |
|---|---|---|
| `slow.semantic.text` | `opencode-go` | `deepseek-v4.1-flash` |
| `slow.semantic.visual` | `opencode-go` | `deepseek-v4-flash-vision-exp` |

These exact IDs are declared by the DSH profile catalogue. Declaration alone is not treated as remote capacity or semantic success.

## Live probes

### Text

```text
UNICLAW_DSH_E2E_BASE=http://127.0.0.1:3080/
UNICLAW_DSH_E2E_MODEL=deepseekV41
DshOpenedHttpPeerE2eTests.E2E_Consult_ChannelRoundTrips_FailsClosedWithoutInventedDecisions
→ PASS
```

The request selected provider `opencode-go` and model `deepseek-v4.1-flash` through the existing authenticated DSH decision channel and returned a Product-valid decision. No API key crossed the Product boundary.

### Vision transport

The DSH plugin and peer now expose a dedicated `/api/uniclaw-agent/slow` route. It creates a `uniclaw-slow` session with the Product decision tool surface excluded, selects the exact model, accepts native same-capture PNG content (`mediaType: image/png`, base64 payload), captures assistant text, and returns it without promoting it to Product authority.

The deployed plugin was restarted on port 3080 and the exact vision model returned a structured JSON object with the expected `Succeeded` status. The earlier `no-submit-decision` result came from the old decision route carrying an incompatible `submit_decision` schema; it is no longer used by Slow calls.

## Baseline evidence

```text
API_LEVEL=35
HOST_LIVE_FULL=PASS
TYPED_LIVE_CHAIN=PASS
COORDINATE_GATE=PASS
CLEANUP=PASS
FINAL_STATUS=PASS
```

## Gate

`slow.semantic.text`: live DSH route verified.

`slow.semantic.visual`: exact DSH route, no-tool preset, same-capture PNG, and structured JSON response verified. The peer returns raw structured text; deterministic Kernel parser/P2 tests remain the only Product authority ingress, and no DSH route writes Product state.
