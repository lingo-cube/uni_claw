# AGT-002 — Real DSH Authenticated Bridge & Real-Model E2E Evidence

> Date: 2026-09-25 · Change: AGT-002 (continuation: F1–F4 fixes + 3080
> authenticated peer + real-model E2E) · No credentials are recorded here.

## 1. Environment

| Item | Value |
|---|---|
| uni_claw commit base | `534d0aef` (+ this slice's commits) |
| DSH runtime | local checkout `/Users/fran/Documents/Code/dk-harness`, profile `web` (bundle `dsh-web-app`), same server binary as the owner's 3080 instance |
| E2E service endpoint | `http://127.0.0.1:3081/` — a second `dsh web` instance (same web profile, `--no-open --port 3081`, PID logged at `/tmp/dsh-3081.log`); the owner's live 3080 instance loads the same profile patch on its next restart |
| Transport auth | DSH browser-session cookie `dsh-auth-<sha256(authority)>` minted from the harness-home signing secret (`~/.dsh/.credentials.yaml`, record `client-connection/browser-session`) — the same programmatic pattern DSH's own host tests use; no launch token, no browser, no stdio |
| Profile identity | profileId `uniagent-prod` · profileVersion `1` · capabilities `[submit_decision]` · capabilityManifestHash `ba8855e41db09771081a7d217850bf99e263ddd84b30d5d076e4df245e1fd637` · protocol `uniclaw.agent.protocol.v1` · schema `uniclaw.agent.schema.v1` |
| DSH-side plugin | `@uniclaw/dsh-decision-channel` v0.1.0 (repo: `dsh/uniclaw-decision-channel/`), wired in `~/.dsh/profiles/web/cordis.patch.yml`; registers the `submit_decision` tool and `/api/uniclaw-agent/{handshake,consult,abort}` on the authenticated /api lane |
| .NET transport | `DshOpenedHttpPeer` (`src/UniClaw.Agent.Dsh/DshOpenedHttpPeer.cs`) — the single sanctioned concrete `IDshOpenedChannelPeer` (closure-guarded) |

## 2. Handshake (frozen-stamp) — PASS

```
POST /api/uniclaw-agent/handshake (cookie-authenticated)
→ {"accepted":true,
   "protocol":{"protocolVersion":"uniclaw.agent.protocol.v1","schemaVersion":"uniclaw.agent.schema.v1",
               "schemaHash":<echoed .NET ProductProtocolSchema hash>,
               "profileId":"uniagent-prod","profileVersion":"1",
               "capabilityManifestHash":"ba8855e…637"},
   "reportedCapabilities":{"capabilities":["submit_decision"]},
   "dshSessionId":"session-415884d4-b47b-4ac2-b568-85703532f632"}
```

- `ProductHandshake.Validate` accepts (F1 mandatory fields enforced; negative:
  profile drift → `profile-id-mismatch`, attach refused).
- 1 Product Session : 1 DSH Session (plugin-owned, lazily created).
- xUnit: `E2E_Handshake_AcceptedOnRealDsh_WithFrozenStamp` +
  `E2E_Handshake_RejectsProfileDrift_OnRealDsh` — PASS.

## 3. Free/local real-model E2E — PASS

Default deployment model (uniagent-prod.yaml → `opencode-go/space-bunny-free`).

**Full .NET stack capture** (adapter → channel → peer → authenticated /api →
plugin → real DSH session → real model → `submit_decision` → normalized
Product AgentDecision → .NET converter):

```
[uniclaw-decision-channel] consult captured
    { requestId: 'dsh-req-00000001', kind: 'act', durationMs: 5760 }
submit_decision raw args:
  {"kind":"act","decisionId":"decision-e2e-1",
   "proposal":{"steps":[{"targetRole":"switch","targetDescriptor":"WiFi",
                          "effectClass":"tap","desiredState":"on"}],
               "justification":"Tap the WiFi switch to turn it on."}}
```

- Real model invocation: yes (session journal turn, tool call executed in-host).
- `submit_decision` called: yes (capture source `submit_decision`).
- DecisionId matched: `decision-e2e-1` == context DecisionId (D2 echo).
- Schema valid: Product `AgentDecisionJsonConverter` round-trip; adapter
  `Assert.Equal("decision-e2e-1", …TryGetDecisionId)`.
- xUnit: `E2E_Consult_ChannelRoundTrips_FailsClosedWithoutInventedDecisions` —
  PASS (5.7 s).

**§11 Policy demo shape (manual consult, same stack below the adapter):**
context objective `cool-to-20`, claim `hvac.temp=24`, mandatory obligation
`hvac.temp=20`, element `toggle/Cooler`, allowedEffects `[tap]`:

```
decision: {"kind":"policy","decisionId":"decision-policy-demo-5",
 "proposal":{"policyId":"policy-1",
   "match":[{"kind":"ClaimEquals","subject":"hvac.temp","value":"24"}],
   "template":{"targetRole":"toggle","targetDescriptor":null,"effectClass":"tap","desiredState":"20"},
   "termination":[{"kind":"ClaimEquals","subject":"hvac.temp","value":"20"}],
   "guards":[],"maxApplications":3,
   "justification":"Apply the available Cooler control as a bounded rule until
    the required temperature is reached."}}
durationMs: 21721, source: submit_decision
```

The real model returned a semantically correct bounded Policy (match domain,
termination at 20, bounded applications, toggle/tap template). Per-application
fresh-chain execution (Kernel PolicyExpand) is exercised deterministically by
the P1–P11 simulation matrix; this E2E proves the *decision channel* delivers
the Policy decision intact.

## 4. DeepSeek Flash E2E — MODEL_CAPABILITY_FAIL (transport PASS)

Config-only switch (`modelSelection.choices.deepseekFlash` /
`UNICLAW_DSH_E2E_MODEL=deepseekFlash`): handshake PASS, session+turn PASS,
fail-closed responses (2 consecutive `no-submit-decision` @ ~6 s — the model
answered prose instead of calling `submit_decision` on this route today).
No protocol/schema change was made to accommodate the model
(`AGENT-002 §9`: MODEL_CAPABILITY_FAIL is an allowed outcome when
transport/profile/schema PASS — they do).

## 5. Failure E2E — PASS (fail-closed, zero invented decisions)

- Turn deadline: generic consult on the free route hit the adapter turn
  boundary → `turn-timeout` after 75 003 ms, adapter returned `null`
  (no decision), late peer task quarantined; zero unauthorized effects
  (no Product effect path is reachable from the decision channel).
- `E2E_Abort_Mechanical_AbortWithoutAttachmentIsHarmless` — PASS.
- Credential/auth failure mapping: 401/403 → `dsh-web-auth-failed:{status}`
  (deterministic unit coverage in `DshOpenedHttpPeerTests`; fail closed, no
  stdio fallback anywhere — closure test enforces it).

## 6. Identity correlation (recorded per §14)

| Field | Value |
|---|---|
| ProductSessionId | `e2e-product-session-1` |
| DshSessionId | `session-415884d4-b47b-4ac2-b568-85703532f632` (handshake) / per-instance sessions `session-b18c16de…`, `session-426ee774…`, `session-415884d4…` |
| RunId | `e2e-run-1` |
| DecisionId / generation | `decision-e2e-1` gen 1 (Act capture) · `decision-policy-demo-5` gen 5 (Policy demo) |
| Provider/model | `opencode-go/space-bunny-free` (default route) |
| Latency | Act 5.8 s · Policy 21.7 s · deepseek-flash fail-closed ~6 s |
| Tokens | not surfaced by the decision channel (diagnostic projection only) |
| Result | Authenticated Product channel PASS · free real-model E2E PASS · deepseek-flash MODEL_CAPABILITY_FAIL (transport PASS) |

## 7. Owner actions / deployment notes

1. The owner's live `127.0.0.1:3080` instance picks up
   `@uniclaw/dsh-decision-channel` on its **next restart** (patch row already
   present in `~/.dsh/profiles/web/cordis.patch.yml`; package installed in the
   profile workspace). The plugin registers the `submit_decision` tool host-wide
   and three authenticated routes; it holds no Product authority.
2. E2E instance on 3081 can be stopped with `pkill -f "port 3081"`.
3. deepseek-flash tool-calling should be retried when the route's model
   behavior changes; no accommodation is permitted at the protocol level.
