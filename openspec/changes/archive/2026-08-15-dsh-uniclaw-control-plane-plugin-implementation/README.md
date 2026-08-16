# README: dsh-uniclaw-control-plane-plugin-implementation

Implementation slice building the minimum real chain
DeepSeek Harness → `dsh-plugin-uniclaw` → UniClaw integration adapter →
DriverHost → UniClaw Kernel, within the frozen protocol baseline.

- **Authoritative baseline (archived, read-only):**
  `openspec/changes/archive/2026-08-15-dsh-uniclaw-control-plane-protocol-baseline/`
- **Graduation record of the baseline:**
  `docs/decisions/dsh-uniclaw-control-plane-protocol-baseline-graduation.md`
- **Pinned DSH:** commit `47f943859bef60e4160492346772ded9b24f765a` (0.1.0-rc.5),
  read-only checkout.
- **Status during Apply:** active change, NOT archived. Maturity claim at
  completion: `DSH_UNICLAW_CONTROL_PLANE_PLUGIN_IMPLEMENTED` only.
- **Next gate:** `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PLUGIN_GRADUATION_REVIEW`.

## Documents

- [proposal.md](proposal.md) — problem, scope, non-goals, authority
- [design.md](design.md) — module layout, transport decision, control audit,
  read semantics, durability, PLUG-F gate matrix, validation
- [specs/dsh-uniclaw-control-plane-plugin-implementation/spec.md](specs/dsh-uniclaw-control-plane-plugin-implementation/spec.md) — ADDED requirements
- [tasks.md](tasks.md) — implementation checklist (system of record)
