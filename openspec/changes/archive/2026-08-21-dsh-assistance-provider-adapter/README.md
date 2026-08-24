# Change: dsh-assistance-provider-adapter

> **IMPLEMENTED** harness-side Assistance provider adapter (APPLY gate executed
> 2026-08-17; pending graduation review) — the DSH plugin
> becomes the concrete `IAssistanceProvider` implementation for the Runtime
> Assistance seam (L1 CONSULT, External Contract Plane 3).

## What it defines

The cross-process direction (bounded pending registry + `assistance.pending` poll
+ `assistance.resolve` submit over the EXISTING DSH→DriverHost connection — no
reverse connection), the DriverHost-side `AssistanceWireProvider` (transport only,
owns NO intelligence), the plugin-side **`AssistanceBridge`** (provider-agnostic
protocol translator — submits to an AVAILABLE Harness consumer, never to a
hard-coded model), the **Harness Intelligence Consumer** (DSH side — actually
solves the request; selection belongs to the Harness), boundedness (capacity 8 +
timeout 30s as COMPOSITION_POLICY ⇒ fail-closed), and the authority/isolation
boundaries (resolve writes only the pending reply; LLM confined to the plugin as an
optional consumer; Runtime untouched).

**Boundary repair**: the transport adapter MUST NOT become the intelligence
decision layer. The bridge is provider-agnostic by contract; the first APPLY proves
the model-free cross-process path with a fake/deterministic consumer before any
real Harness consumer is attached.

## Scope guardrails

- **Implemented pending graduation**: the APPLY implementation is complete;
  graduation and archive remain a separate review gate.
- **Runtime untouched**: the seam already exists; this gate changes nothing there (F7).
- **No reverse connection**: poll/resolve reuse the existing connection direction (F1).
- **Adapter ≠ intelligence layer**: bridge owns no semantic policy/routing/recovery (F11).
- **Bridge provider-agnostic**: no direct `ctx.get('llm')` requirement; LLM is one
  optional consumer implementation (F2).
- **First APPLY model-free**: fake/deterministic consumer (F12).
- **Advice never authority / never writes Kernel state** (F3/F4).
- **Bounded consult as COMPOSITION_POLICY** (F5); **frozen wire preserved** (F6);
  **no new emitters** (F8); **capability-gap vocabulary, not a prompt** (F9).

## Documents

- `proposal.md` — buyer/gap/direction-decision/scope/falsifiers/authority
- `design.md` — direction analysis (Option B chosen), wire design, provider shape,
  plugin service, boundedness, authority, test plan T1–T9
- `specs/dsh-assistance-provider-adapter/spec.md` — requirements + scenarios
- `tasks.md` — baseline slices + APPLY implementation plan

## Next gate (planning context)

After BASELINE validates and the buyer confirms: `PROJECT_LEADER_APPLY_DSH_ASSISTANCE_PROVIDER_ADAPTER`
(implementation), then Guidance (Plane 4) / Execution Handoff (Plane 5) far-term.
