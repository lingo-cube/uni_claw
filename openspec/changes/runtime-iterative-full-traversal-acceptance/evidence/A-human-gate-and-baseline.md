# A. Implementation Human Gate — Evidence Record

## A.1 Human Approval (Phase 2.6 Implementation Gate)

- **Decision**: `Phase26Implementation: APPROVED` (Human, 2026-08-24 session; delivered as the
  direct human instruction header `PROJECT_LEADER_UNIFLOW_APPLY_RUNTIME_ITERATIVE_FULL_TRAVERSAL_ACCEPTANCE`).
- **Change**: `runtime-iterative-full-traversal-acceptance`
- **RealEmulatorCampaign**: AUTHORIZED
- **PhysicalDevice**: DEFERRED · **Phase3Memory**: DEFERRED · **RuntimeChange**: NOT_AUTHORIZED
- **Approved artifact digests** (SHA-256, recorded before any implementation edit):

| Artifact | SHA-256 |
|---|---|
| `proposal.md` | `11142565297f82c78384a56771684299c785212b8f1c00aa6f18fbe8d16a7a39` |
| `design.md` | `51fdf984dc444fb608817c26d506ffae3644bdc8a5a1e969a0fec7d0deb1bfdf` |
| `tasks.md` | `2368b5cab87a3f17f55014f14268408acfad22d4860cf3c69c1f8ab4dd112dcf` |
| `specs/runtime-iterative-full-traversal-acceptance/spec.md` | `3a4e9858d00bf304bd6bc4e34dd74282de7a62d0364142d46fe01909968a4ca8` |

- **Baseline git revision**: `f81ecf955935692fd009493ef324c67f820e80f9`
  (`feat(exploration): Archive Runtime Exploration Ledger & Depth Control (Phase 2.6 graduation)` — commit title mentions Phase 2 archive).

### Pre-existing working-tree state (recorded BEFORE this change's first edit)

The working tree already contained unrelated modifications. The byte-identity claim of this
change is measured against THIS starting state, not against HEAD:

- Modified: `.ai/profiles/modules.json`, `.codex/config.toml`, `.dsh/profile-adapter/*`,
  `docs/decisions/index.md`, `docs/decisions/runtime-exploration-roadmap.md`,
  `docs/snapshots/latest.md`, `docs/work/active/*`, one archived-change README/tasks pair,
  `src/UniClaw.Runtime.PhysicalHost/PhysicalHostComposition.cs` (+22/−5, pre-existing),
  `src/UniClaw.Runtime.sln` (+14, pre-existing), deleted `openspec/changes/runtime-exploration-ledger-and-depth-control/*`
  (already archived).
- **Runtime byte-identity guard for THIS change**: `src/UniClaw.Runtime/**`,
  `src/UniClaw.Runtime.Adapters/**`, `src/UniClaw.Runtime.DriverHost/**`,
  `src/UniClaw.Runtime.Harness/**`, `src/UniClaw.Semantic.*/**` must not be further modified
  by this change. Verification: `git diff <baseline-snapshot> -- <paths>` limited to the
  pre-existing diffs above; additionally a SHA-256 manifest of all Runtime production files
  is captured at implementation start (see `runtime-baseline-manifest.sha256`) and re-checked
  at M.1.

## A.2 Owners and Campaign Scope

- **Implementation orchestrator**: DSH main thread (coding-leader role, UniFlow discipline).
- **Implementation workers**: UniFlow module-worker subagents (single-unicast per WorkItem,
  `runtime-integration` / `engineering-governance` module profiles, harness + test paths only).
- **Independent acceptance reviewer (J/N)**: fresh verifier subagent, instructed to re-derive
  from raw artifacts without trusting task checkboxes.
- **Real Emulator campaign scope**:
  - AVD: `scroll-test` (the only AVD on this host; Android emulator image).
  - App: **real Android Settings** (`com.android.settings`) — the Phase 2.6 scenario scope;
    the Phase 2.5 `com.uniclaw.fixture` capstone app remains available for Tier-B smoke reuse
    but is NOT this change's scenario.
  - Perception: production pipeline (CanonicalVisionHostFactory UDS vision host +
    `AdbScreenshotSource` + `AdbDispatchTarget`), vision-only primary semantics per the
    graduated capstone composition.
  - adb: `/opt/homebrew/share/android-commandlinetools/platform-tools/adb`; serial `emulator-5554`.
  - No physical device. No Phase 3 Memory.
- **Confirmation (A.2 stop-check)**: no task in `tasks.md` B–N requires Runtime modification,
  new wire/API, Strategy Contract change, GoalEvidence/FSM authority change, SourceIdentity
  semantics change, Memory service, dynamic depth, or mid-run replanning. All required runtime
  mechanisms (recursive descent, verified parent return, scroll exhaustion, identity-exact
  ledger, boundary disposition, depth semantics) are already graduated per
  `docs/decisions/runtime-full-traversal-acceptance-analysis.md`. Proceed.

## Asset layout decision (implementation-time, per proposal "Impact")

- Persisted knowledge fixtures: `validation/knowledge/settings/<scenario-id>/v<N>/`
  (human-readable JSON records + generated Markdown digest; deterministic, diffable,
  versioned, scope-explicit).
- Campaign evidence (this change): `openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/`.
- New harness code: `src/UniClaw.Runtime.ValidationHarness/{Campaign,Knowledge,Planning,SettingsBinding}/`.
- New tests: `tests/UniClaw.Runtime.Tests/ValidationHarness/`.
