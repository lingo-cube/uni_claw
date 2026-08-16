# dsh-plugin-uniclaw

DeepSeek Harness → UniClaw control-plane adapter (bounded vertical slice).

This plugin implements the minimum real chain **DeepSeek Harness → this
plugin → UniClaw DriverHost** for the frozen protocol baseline
`dsh-uniclaw-control-plane-protocol-baseline`, plus the Shadow Cognition
vertical slice (`dsh-shadow-cognition`, V1 — `EPHEMERAL_PROCESS_LOCAL`):

- **DSH plugin lifecycle** — cordis-fork activation guard, `session/created`
  connection trigger, `session/event` firehose subscription, deterministic
  command registration, `ctx.effect` cleanup.
- **DriverHost connection boundary** — loopback TCP, newline-delimited
  JSON-RPC, bounded reconnect, fresh-state guarantee (nothing cached across
  connections).
- **Read-only consumption** — classified run snapshots, runtime events,
  active traps, and logical evidence refs. No Kernel-mutating authority
  exists anywhere in this module.
- **Deterministic human control seam** — control operations are audited via
  `control.support` and reported as deferred
  (`DEFERRED_NO_KERNEL_CONTROL_BUYER`) when the Kernel has no buyer.
- **Shadow Cognition (V1)** — `uniclaw-shadow-analyze`: truthful DSH session
  identity → deterministic UniClaw read-only retrieval → bounded context
  assembly → OPTIONAL one `ctx.llm` call → `ShadowAnalysis` classified
  `COGNITIVE_INFERENCE` → structured command response → optional bounded
  process-local cache. ZERO custom session events; nothing written back to
  the Kernel; auto triggers deferred (`shadow.autoTriggers` MUST be `[]`).

## Design constraints (frozen baseline)

- The pinned DSH baseline is commit `47f943859bef60e4160492346772ded9b24f765a`
  (`@deepseek-ai/dsh-root` `0.1.0-rc.5`), and this plugin requires the vendored
  cordis fork `@deepseek-ai/cordis` `4.0.1` exactly; activation refuses any
  other version.
- The plugin never dispatches an action, never writes a durable session
  event, and (outside the shadow command's optional model seam) never calls
  an inference service. Consumption is read-only and lifecycle events are
  plugin-owned live events only.
- The shadow modules may reference `llm`/`model` by design (the optional
  `ctx.llm` seam); the control plane (`src/adapter.js`, `src/protocol.js`)
  stays inference-free (enforced by the lifecycle test).

## Layout

```
src/protocol.js         wire contract helpers (encoding, typed error codes)
src/adapter.js          UniClawAdapter — TCP JSON-RPC client, bounded reconnect
src/commands.js         five deterministic read-only DSH commands
src/plugin.js           cordis plugin: activation guard, lifecycle, service,
                        commands, shadow wiring, cleanup
src/shadow/analysis.js  ShadowAnalysis schema/builder, classification + uncertainty vocab
src/shadow/context.js   deterministic bounded context assembler (snapshot/events/trap/evidence)
src/shadow/model.js     one-shot ctx.llm seam (timeout/error mapping, analyst prompt)
src/shadow/cache.js     bounded process-local cache (EPHEMERAL_PROCESS_LOCAL)
src/shadow/index.js     orchestrator + frozen config validation
test/                   node:test suite + e2e-client.mjs (driven by the .NET E2E)
```

## Commands

Registered through the dsh-commands API, whose name contract
(`/^[a-z][a-z0-9_-]*$/`) forbids dots — hence the hyphenated namespacing:

| command | purpose |
|---|---|
| `uniclaw-inspect-run <runId>` | classified read-only snapshot |
| `uniclaw-inspect-trap <runId>` | classified active trap |
| `uniclaw-evidence-open <locator> <runId>` | logical evidence ref (metadata only) |
| `uniclaw-runs-list` | registered run ids |
| `uniclaw-shadow-analyze <runId> [--focus general\|trap\|failure\|completion\|progress\|blocked] [--reason <text>]` | bounded `ShadowAnalysis` (COGNITIVE_INFERENCE) — zero-model dispatch, optional one `ctx.llm` call |

The four control-plane handlers are pure reads: they format wire DTOs and
return a `CommandResult`; none ever calls out to any inference service. The
shadow command runs its handler directly against the receiving agent (the
command is never sent to the model), retrieves deterministically FIRST, and
makes at most one `ctx.llm` call when `shadow.model.provider`/`model` are
configured; otherwise it returns a deterministic zero-model digest with
`uncertainty: model-unavailable`.

## Running the tests

```bash
node --test dsh-plugin-uniclaw/test/
```

The lifecycle tests load the vendored cordis fork directly; set
`DSH_TEST_CORDIS_PATH` to its `lib/index.js` when it is not installed under
the repository's `node_modules/@deepseek-ai/cordis`.
