# Tasks: dsh-uniclaw-control-plane-protocol-baseline

> OpenSpec-only change (SOURCE_FIRST_ARCHITECTURE_AUDIT_AND_OPENSPEC). No production code, no DSH
> mutation, no Runtime mutation, no plugin implementation. Verification = `openspec validate --strict`
> + `scripts/check-consistency.sh` (no Runtime build/test required).

## 1. Pin DSH Compatibility Baseline

- [x] 1.1 Pin `UNICLAW_DSH_COMPATIBILITY_BASELINE = 47f943859bef60e4160492346772ded9b24f765a`
  (DSH `0.1.0-rc.5`, pre-release, no git tags on pinned checkout, release commit `abe560f81e` in history,
  remote `deepseek-ai/deepseek-harness`, branch `master`).
  - Implementation: recorded in design.md §0, proposal.md, spec.md.
  - Invariant Verification: F1 falsifier — version is pinned, not floating.
  - Test Verification: baseline block cites exact commit + version + tag-absence note.

## 2. Source-First DSH Audit (READ-ONLY)

- [x] 2.1 Audit DSH plugin + service surfaces (plugin lifecycle, registration, service registration,
  config, permissions).
  - Source: `vendor/cordis/src/{registry,service,context,events,fiber}.ts`, `packages/boot/app-boot`,
    `packages/bundle/*/cordis.patch.yml`, `packages/preset/agent-presets`, `packages/interaction/permission-presets`,
    `packages/sandbox/*`, `packages/interaction/user-approval`.
  - Invariant Verification: no DSH file modified; docs-vs-source discrepancies recorded (D1, D2, D7).
  - Test Verification: subagent report settled (fd265913) + personal verification of S27–S35.

- [x] 2.2 Audit DSH session + persistence surfaces (session lifecycle, durable log, persistence/reload/
  resume/replay/fork, transport, streaming).
  - Source: `packages/core/session/*`, `packages/session/*`, `packages/sdk/*`, `packages/acp/*`,
    `packages/client/connection/*`, `packages/host/apiproxy/*`.
  - Invariant Verification: no DSH file modified; discrepancies recorded (D4, D5).
  - Test Verification: subagent report settled (2df1054d) + personal verification of S1–S5, S15–S22.

- [x] 2.3 Audit DSH agent + commands + tools surfaces (agent lifecycle/events/hooks, commands, tools,
  cancellation/timeout/error).
  - Source: `packages/core/agent/*`, `packages/interaction/commands/*`, `packages/core/tools/*`,
    `packages/core/scope/*`.
  - Invariant Verification: command handler-without-model semantics verified from source (S7);
    discrepancy D3 recorded.
  - Test Verification: subagent report settled (171db725) + personal verification of S6–S10, S37–S38.

- [x] 2.4 Audit DSH client + workflow surfaces (client/UI modules, workflow/subagent orchestration).
  - Source: `packages/client/*`, `packages/workflow/*`, `packages/subagent/*`, `packages/skills/*`,
    `packages/jobs/*`.
  - Invariant Verification: client-module model verified (dsh.client roster → `window.__DSH_BOOT__`);
    discrepancy D6 recorded.
  - Test Verification: subagent report settled (16e619f2) + personal verification of S11–S12, S25–S26.

## 3. Synthesize Matrices

- [x] 3.1 Write SourceEvidenceMatrix (`source-evidence-matrix.md`): 43 rows (S1–S43), each with Source File /
  Type/API/Event / Semantics / Durable? / Model-facing? / Lifecycle / Stability / UniClaw Use; plus
  docs-vs-source discrepancy table D1–D7.
  - Invariant Verification: every row traceable to pinned checkout; no row cites latest docs.
  - Test Verification: cross-checked against the four subagent reports and personal reads.

- [x] 3.2 Write IntegrationMatrix + DecisionTable (`integration-matrix.md`): required rows
  (RuntimeEvent, RunSnapshot, EvidenceRef, 10 event kinds, 11 human control ops, 7 cognitive op groups,
  Shadow insertion point, Control Plane UI) with Direction / Durable-Live-Read-only / Model Involved /
  Authority / Freshness / Adapter Required / Status / DSH Source Evidence; DecisionTable with
  `NATIVE_DSH_SEAM_CONFIRMED` / `NATIVE_DSH_SEAM_WITH_ADAPTER` / `NO_NATIVE_SEAM_FOUND` /
  `DEFERRED_NEEDS_BUYER` / `PROTOCOL_PRESSURE`; hard-forbidden paths.
  - Invariant Verification: F4 — no parallel protocol; every row maps to a DSH-native surface.
  - Test Verification: every required row present and source-cited.

## 4. Write OpenSpec Change Artifacts

- [x] 4.1 `proposal.md` — Why / What Changes / Capabilities / Impact / Non-Goals.
- [x] 4.2 `design.md` — pinned baseline (§0), authority planes (§1), component roles (§2),
  extension-point audit (§3), observability mapping (§4), RunSnapshot read paths (§5),
  EvidenceRef mapping (§6), human control (§7), cognitive ops (§8), token economy (§9),
  protocol-gap policy (§10), Shadow insertion point (§11), Advisory boundary (§12),
  model-facing tool evaluation (§13), UI mapping (§14), transport decision (§15),
  process lifecycle (§16), falsifiers (§17), graduation criteria (§18), future sequence (§19).
- [x] 4.3 `specs/dsh-uniclaw-control-plane-protocol-baseline/spec.md` — ADDED requirements with
  scenarios (baseline pin, source evidence, no parallel protocol, Kernel authority, no second mutable
  owner, model-free human control, buyer-gated durability, transport deferral, process lifecycle,
  Shadow insertion point, Advisory metadata seam, token economy, EvidenceRef logical identity,
  UI-as-projection, roles frozen, artifacts validated).
- [x] 4.4 `tasks.md` — this file.
- [x] 4.5 `README.md` + `.openspec.yaml` per repo convention.

## 5. Validate

- [x] 5.1 Run `openspec validate dsh-uniclaw-control-plane-protocol-baseline --strict --no-interactive`.
  - Invariant Verification: PASS required; fix any validation error before proceeding.
  - Test Verification: recorded PASS output.
- [x] 5.2 Run `scripts/check-consistency.sh`.
  - Invariant Verification: ALL PASS required (this OpenSpec-only change requires no Runtime build/test).
  - Test Verification: recorded PASS output.

## 6. Emit Result

- [x] 6.1 Emit `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_RESULT` with all ~40 fields,
  Status `BASELINE_READY_FOR_REVIEW`, TargetMaturity `DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_FROZEN`,
  NextGate `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_REVIEW`, ParallelProtocolInvented=NO,
  DirectDSHPhysicalAuthority=NO.
- [x] 6.2 End STOP. No plugin, Shadow, Advisory, C-class emitter, transport, or UI implementation begins.
