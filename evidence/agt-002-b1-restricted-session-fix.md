# AGT-002 — B1 Restricted Session Fix Evidence (allow-form restriction)

> Date: 2026-09-26 · Change: AGT-002 review B1 (follow-up fix round)
> Reference: `docs/analysis/dsh-tool-scope-restricted-sessions.md`
> Instruction: `docs/analysis/agt-002-b1-fix-instruction.md`
> No credentials recorded.

## Fix shape (per reference §4)

```
preset scope (uniagent-prod standing scope)
├── @uniclaw/dsh-uniagent-restrict   → ctx.tools.restrict({ allow: [] })
└── @uniclaw/dsh-decision-channel    → ctx.tools.guard(…) (execution-time second line)

agent scope (Agent joined to the preset; child of preset scope)
└── submit_decision                  ← registered at handshake into agent.ctx
```

Semantics relied upon (all source-cited in the reference):
- deny-only filters admit later unlisted inherited tools (`docs/subsystems/tools.md:165`)
- restrictions filter the inherited surface incl. the global layer
  (`packages/core/tools/src/index.ts:1187-1201`)
- only the viewing scope's OWN layer is exempt (`:1202-1209`) — hence the
  tool must live one layer deeper (agent scope) than the restriction
  (preset scope).
- `restrict()` validates names against the inherited surface only, so
  `allow: ['submit_decision']` is impossible — the manifest is expressed as
  empty allow + child-layer registration.

## M1 — mechanism tests

```
node --test dsh/uniclaw-decision-channel/tests/plugin.test.mjs   → pass 12 / fail 0
node --test dsh/uniclaw-restrict/tests/restriction.test.mjs     → pass 5 / fail 0
```

restriction.test.mjs loads the checkout's built ToolRuntime and covers: (1)
allow[] in preset + tool in agent survives late MCP registration; (2) REGRESSION:
old deny-snapshot leaks late tools; (3) REGRESSION: same-layer restriction
empties the catalog; (4) guard rejects non-manifest calls; (5) ordinary
sessions keep the full surface.

## M2 — real instance end-to-end (decisive)

Instance: fresh `dsh web --no-open --port 3082` on the owner profile with the
reinstalled (snapshot-verified via grep: `ctx.tools.restrict({ allow: [] })`
at installed src index.js:58) plugin pair.

MCP servers present (precondition — proof is only meaningful with tools in the field):

```
csharper-mcp        connected  toolCount: 8
cwm-roslyn-navigator connected toolCount: 15
TOTAL: 23
```

Handshake (cookie-authenticated, frozen stamp, real schemaHash):

```
[uniclaw-decision-channel] submit_decision registered into the restricted agent scope
[uniclaw-decision-channel] restricted session verified: visible tools = submit_decision
[uniclaw-decision-channel] handshake accepted
→ {"accepted":true, …, "runtimeCapabilities":["submit_decision"],
   "runtimePreset":"uniagent-prod","dshSessionId":"session-c55…"}
```

## M3 — counter-proof (old mechanism must fail under identical conditions)

The restrict plugin was temporarily swapped to the old deny-snapshot build
(`deny: [<boot-time global names>]`), reinstalled (snapshot grep confirmed
`M3-OLD-MECHANISM` marker present), instance restarted, same 23 MCP tools
connected:

```
[M3-OLD-MECHANISM] denied boot-time snapshot: 2 entries
→ handshake 200:
  {"ok":false,"error":{"code":"session-capability-mismatch",
   "message":"actual=[mcp__csharper-mcp__apply_code_action, …
   mcp__cwm-roslyn-navigator__find_references, … (23 mcp__* entries) …,
   submit_decision] expected=[submit_decision]"}}
```

The old mechanism's boot snapshot captured only the 2 usage_* globals; the 23
asynchronously-registered MCP tools leaked through, exactly as the documented
deny semantics predict. Fixed build restored, reinstalled (grep re-verified
`allow: []`), restarted, and re-probed under the same 23-tool field:

```
RESTORED HANDSHAKE 200 {"accepted":true, …, "runtimeCapabilities":["submit_decision"] …}
MCP total tools still present: 23
```

## M4 — disposition

- The late-deny workaround (`state.restrictCtx`) and all boot-time deny
  snapshots are gone from the source (deny appears only in explanatory
  comments); the handshake verification is a pure check with no patching.
- The dedicated per-session workspace remains ONLY as filesystem isolation
  (no read/write of the operator's repos), with comments stating it is NOT a
  tool-visibility mechanism (reference §3 disproved cwd-based visibility).
- Dead-path residue: `dsh/agent-presets/uniagent-prod/` files are marked
  reference-only (DSH reads preset declarations exclusively from profile
  patch rows).
- Test instance on 3082 stopped after evidence capture.
