# Current Gates

DocumentType: `CURRENT_STATE_PROJECTION`
Authority: `NONE`
GeneratedProjection: `true`
GenerationRule: `openspec list --json` plus direct active proposal membership and archive directory inventory
GeneratedAt: `2026-08-31`
ProjectionState: `CURRENT`
ActiveChangeCount: `13`
ArchivedChangeCount: `81`

This is generated projection data derived from the OpenSpec directory state. It
does not establish lifecycle authority, change an OpenSpec lifecycle, or replace
the source artifacts cited below. The frozen governance rule is
[repository-governance-authority-baseline](../../decisions/repository-governance-authority-baseline.md).

## Source precedence

1. Direct child change bundles under `openspec/changes/` containing
   `proposal.md`, excluding `archive/`.
2. Current OpenSpec `tasks.md` artifacts and `openspec list --json`.
3. Gate and graduation receipts for classification only; they do not change
   active membership.
4. The current `openspec/changes/archive/` directory inventory.

The [2026-08-16 lifecycle matrix](../../decisions/active-openspec-lifecycle-matrix.md)
is a historical snapshot. It is retained as evidence and is not the source for
this current projection.

## Generated Active Change Membership — 13

| Change | Source reference |
|---|---|
| `container-runtime-v2-core-semantics` | [proposal](../../../openspec/changes/container-runtime-v2-core-semantics/proposal.md) · [tasks](../../../openspec/changes/container-runtime-v2-core-semantics/tasks.md) |
| `container-runtime-v2-evidence-model` | [proposal](../../../openspec/changes/container-runtime-v2-evidence-model/proposal.md) · [tasks](../../../openspec/changes/container-runtime-v2-evidence-model/tasks.md) |
| `perception-ocr-en-v4-normalization` | [proposal](../../../openspec/changes/perception-ocr-en-v4-normalization/proposal.md) · [tasks](../../../openspec/changes/perception-ocr-en-v4-normalization/tasks.md) |
| `perception-operator-rule-framework` | [proposal](../../../openspec/changes/perception-operator-rule-framework/proposal.md) · [tasks](../../../openspec/changes/perception-operator-rule-framework/tasks.md) |
| `runtime-active-container-context-and-transition-semantics` | [proposal](../../../openspec/changes/runtime-active-container-context-and-transition-semantics/proposal.md) · [tasks](../../../openspec/changes/runtime-active-container-context-and-transition-semantics/tasks.md) |
| `runtime-agent-pre-terminal-cycle-contract` | [proposal](../../../openspec/changes/runtime-agent-pre-terminal-cycle-contract/proposal.md) · [tasks](../../../openspec/changes/runtime-agent-pre-terminal-cycle-contract/tasks.md) |
| `runtime-debug-post-graduation-conformance-repair` | [proposal](../../../openspec/changes/runtime-debug-post-graduation-conformance-repair/proposal.md) · [tasks](../../../openspec/changes/runtime-debug-post-graduation-conformance-repair/tasks.md) |
| `runtime-external-semantic-capability-boundary` | [proposal](../../../openspec/changes/runtime-external-semantic-capability-boundary/proposal.md) · [tasks](../../../openspec/changes/runtime-external-semantic-capability-boundary/tasks.md) |
| `runtime-iterative-full-traversal-acceptance` | [proposal](../../../openspec/changes/runtime-iterative-full-traversal-acceptance/proposal.md) · [tasks](../../../openspec/changes/runtime-iterative-full-traversal-acceptance/tasks.md) |
| `semantic-perception-contract-baseline` | [proposal](../../../openspec/changes/semantic-perception-contract-baseline/proposal.md) · [tasks](../../../openspec/changes/semantic-perception-contract-baseline/tasks.md) |
| `semantic-perception-layer-baseline` | [proposal](../../../openspec/changes/semantic-perception-layer-baseline/proposal.md) · [tasks](../../../openspec/changes/semantic-perception-layer-baseline/tasks.md) |
| `trace-capture-scenario-catalog-foundation` | [proposal](../../../openspec/changes/trace-capture-scenario-catalog-foundation/proposal.md) · [tasks](../../../openspec/changes/trace-capture-scenario-catalog-foundation/tasks.md) |
| `uniagent-local-exploration-memory` | [proposal](../../../openspec/changes/uniagent-local-exploration-memory/proposal.md) · [tasks](../../../openspec/changes/uniagent-local-exploration-memory/tasks.md) |

Task progress is read from each linked `tasks.md`; this projection does not
maintain a second aggregate completion/graduation status.

## Gate Annotations

- `perception-navigation-row-composition-repair` is an active Human-authorized
  perception-side IR-G0 repair. Its Runtime/Agent authority delta is NONE. The
  candidate deployment was exercised in an isolated real-emulator campaign
  without mutating CURRENT ACTIVE. Same-frame duplicate rows are repaired. A
  tested three-anchor geometry relaxation promoted `Volume, vibration, Do Not
  Disturb` as a menu item and was reverted; the retained operator requires four
  confirmed anchors and fails closed below that boundary. Detector retraining or
  a dedicated visual Row Grouping / Relation Head is now the Human Gate. XML,
  fuzzy Runtime identity, VLM action authority, and canonical promotion remain
  unauthorized. CURRENT ACTIVE remains unchanged.
- Runtime Exploration Roadmap Phase 2 is **GRADUATED / CHANGE SET ARCHIVED**.
  The predecessor `runtime-exploration-ledger-and-depth-control` and Option A
  successor `runtime-exploration-semantic-admission-remediation` are represented
  by their dated archive bundles below; the graduated capability remains active.
  The 2026-08-25 graduation decision is retained as the pre-archive decision
  record and its `NOT_ARCHIVED` wording is not rewritten here.
  The earlier
  [graduation revocation](../../decisions/runtime-exploration-ledger-and-depth-control-graduation-reverification-decision.md)
  remains historical gap evidence and is superseded as the current lifecycle
  conclusion. Phase 3 Memory, Phase 4 dynamic depth, new wire/schema, new
  Evidence owner/state system, and scenario knowledge remain unauthorized.
  The Phase 2 capability baseline is now frozen as a source-linked projection,
  but the
  [Roadmap consistency analysis](../../decisions/runtime-exploration-roadmap-phase2-consistency-analysis.md)
  found the Roadmap's depth 1/2/N examples inconsistent with approved D1 depth
  0/1/N semantics. Status: `ROADMAP_CONSISTENCY_HUMAN_GATE_REQUIRED` /
  `PHASE3_PREPARATION_PAUSED`; no Phase 3 Ownership Analysis has begun.
- `uniagent-runtimeagent-strategy-contract` is graduated per its
  [graduation decision](../../decisions/uniagent-runtimeagent-strategy-contract-graduation-decision.md)
  (2026-08-24, human-authorized verification path; six forbidden-edge proofs verified).
  Archive is a separate pending lifecycle operation; the change remains listed above
  until archived.
- `uniagent-emulator-validation-harness` is graduated AND ARCHIVED (2026-08-26 unified
  archive) per its
  [graduation decision](../../decisions/uniagent-emulator-validation-harness-graduation-decision.md)
  (capability `PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED`: Tier A deterministic green,
  Tier B Real-Emulator S1 PASS @ 8/8 / S2 PASS_BOUNDED_FAIL_CLOSED / S3 PASS, Tier C Physical
  Device WAIVED_BY_HUMAN — no physical-device claim). Archived bundle:
  `openspec/changes/archive/2026-08-26-uniagent-emulator-validation-harness/`.
- `runtime-iterative-full-traversal-acceptance` is a **proposal only** (created 2026-08-26,
  Human direction APPROVED, OpenSpec creation AUTHORIZED); implementation is NOT authorized
  pending its Phase 2.6 Implementation Human Gate (tasks A.1/A.2). Validation-only change:
  no runtime modification; physical device DEFERRED; Phase 3 Memory DEFERRED until Phase 2.6
  completes.
- `semantic-perception-layer-baseline` remains `APPLY_NOT_AUTHORIZED` by its
  [decision](../../decisions/semantic-perception-layer-baseline.md).
- `trace-capture-scenario-catalog-foundation` retains two open validation tasks;
  see its [implementation review](trace-capture-scenario-catalog-foundation-implementation-review.md)
  and [architecture gate](../../decisions/trace-capture-scenario-catalog-architecture-gate.md).

## Historical Archived — 81

| Archived change | Source reference |
|---|---|
| `2026-08-15-dsh-kernel-read-only-observability` | [archive](../../../openspec/changes/archive/2026-08-15-dsh-kernel-read-only-observability/) |
| `2026-08-15-dsh-shadow-cognition` | [archive](../../../openspec/changes/archive/2026-08-15-dsh-shadow-cognition/) |
| `2026-08-15-dsh-uniclaw-control-plane-plugin-implementation` | [archive](../../../openspec/changes/archive/2026-08-15-dsh-uniclaw-control-plane-plugin-implementation/) |
| `2026-08-15-dsh-uniclaw-control-plane-protocol-baseline` | [archive](../../../openspec/changes/archive/2026-08-15-dsh-uniclaw-control-plane-protocol-baseline/) |
| `2026-08-16-dsh-control-plane-event-stream` | [archive](../../../openspec/changes/archive/2026-08-16-dsh-control-plane-event-stream/) |
| `2026-08-16-open-world-traversal-identity-safety` | [archive](../../../openspec/changes/archive/2026-08-16-open-world-traversal-identity-safety/) |
| `2026-08-16-perception-actionable-toggle-evidence` | [archive](../../../openspec/changes/archive/2026-08-16-perception-actionable-toggle-evidence/) |
| `2026-08-16-perception-actionable-toggle-evidence-reality-repair` | [archive](../../../openspec/changes/archive/2026-08-16-perception-actionable-toggle-evidence-reality-repair/) |
| `2026-08-16-phase1-deterministic-runtime` | [archive](../../../openspec/changes/archive/2026-08-16-phase1-deterministic-runtime/) |
| `2026-08-16-phase2-trap-recovery` | [archive](../../../openspec/changes/archive/2026-08-16-phase2-trap-recovery/) |
| `2026-08-16-phase3-bounded-candidate-safety` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-bounded-candidate-safety/) |
| `2026-08-16-phase3-bounded-cross-page-discovery` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-bounded-cross-page-discovery/) |
| `2026-08-16-phase3-discovered-branch-effect-revalidation` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-discovered-branch-effect-revalidation/) |
| `2026-08-16-phase3-popup-local-recovery` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-popup-local-recovery/) |
| `2026-08-16-phase3-recovery-progress-resume` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-recovery-progress-resume/) |
| `2026-08-16-phase3-s0-capstone-settings-traversal` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-s0-capstone-settings-traversal/) |
| `2026-08-16-phase3-scroll-identity-continuity` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-scroll-identity-continuity/) |
| `2026-08-16-phase3-sibling-branch-progress` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-sibling-branch-progress/) |
| `2026-08-16-phase3-uncertain-action` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-uncertain-action/) |
| `2026-08-16-phase3-viewport-exploration-exhaustion` | [archive](../../../openspec/changes/archive/2026-08-16-phase3-viewport-exploration-exhaustion/) |
| `2026-08-16-physical-scroll-container-semantic-traversal` | [archive](../../../openspec/changes/archive/2026-08-16-physical-scroll-container-semantic-traversal/) |
| `2026-08-16-runtime-observability-trace-foundation` | [archive](../../../openspec/changes/archive/2026-08-16-runtime-observability-trace-foundation/) |
| `2026-08-16-semantic-run-popup-obstruction-integration` | [archive](../../../openspec/changes/archive/2026-08-16-semantic-run-popup-obstruction-integration/) |
| `2026-08-16-semantic-run-unexpected-navigation-reconciliation` | [archive](../../../openspec/changes/archive/2026-08-16-semantic-run-unexpected-navigation-reconciliation/) |
| `2026-08-16-settings-navigation-candidate-evidence` | [archive](../../../openspec/changes/archive/2026-08-16-settings-navigation-candidate-evidence/) |
| `2026-08-16-u2-open-world-settings-traversal` | [archive](../../../openspec/changes/archive/2026-08-16-u2-open-world-settings-traversal/) |
| `2026-08-17-open-world-container-inventory-completeness` | [archive](../../../openspec/changes/archive/2026-08-17-open-world-container-inventory-completeness/) |
| `2026-08-19-settings-full-tree-enumeration-integration` | [archive](../../../openspec/changes/archive/2026-08-19-settings-full-tree-enumeration-integration/) |
| `2026-08-21-agent-concept-model-v1-alignment` | [archive](../../../openspec/changes/archive/2026-08-21-agent-concept-model-v1-alignment/) |
| `2026-08-21-dsh-assistance-provider-adapter` | [archive](../../../openspec/changes/archive/2026-08-21-dsh-assistance-provider-adapter/) |
| `2026-08-21-dsh-runtime-agent-subagent-run-entry` | [archive](../../../openspec/changes/archive/2026-08-21-dsh-runtime-agent-subagent-run-entry/) |
| `2026-08-21-post-action-state-settle` | [archive](../../../openspec/changes/archive/2026-08-21-post-action-state-settle/) |
| `2026-08-21-runtime-assistance-seam` | [archive](../../../openspec/changes/archive/2026-08-21-runtime-assistance-seam/) |
| `2026-08-21-runtime-external-contract-baseline` | [archive](../../../openspec/changes/archive/2026-08-21-runtime-external-contract-baseline/) |
| `2026-08-21-uniagent-decision-goal-evaluation-minimum-contract` | [archive](../../../openspec/changes/archive/2026-08-21-uniagent-decision-goal-evaluation-minimum-contract/) |
| `2026-08-21-uniclaw-driverhost-production-server-mode` | [archive](../../../openspec/changes/archive/2026-08-21-uniclaw-driverhost-production-server-mode/) |
| `2026-08-21-verified-local-continuity` | [archive](../../../openspec/changes/archive/2026-08-21-verified-local-continuity/) |
| `2026-08-21-vision-runtime-bootstrap` | [archive](../../../openspec/changes/archive/2026-08-21-vision-runtime-bootstrap/) |
| `2026-08-24-semantic-evidence-fusion-baseline` | [archive](../../../openspec/changes/archive/2026-08-24-semantic-evidence-fusion-baseline/) |
| `2026-08-26-runtime-exploration-ledger-and-depth-control` | [archive](../../../openspec/changes/archive/2026-08-26-runtime-exploration-ledger-and-depth-control/) |
| `2026-08-26-runtime-exploration-semantic-admission-remediation` | [archive](../../../openspec/changes/archive/2026-08-26-runtime-exploration-semantic-admission-remediation/) |
| `2026-08-26-uniagent-emulator-validation-harness` | [archive](../../../openspec/changes/archive/2026-08-26-uniagent-emulator-validation-harness/) |
| `2026-08-28-runtime-evidence-based-quiescence-admission` | [archive](../../../openspec/changes/archive/2026-08-28-runtime-evidence-based-quiescence-admission/) |
| `2026-08-28-runtime-viewport-exhaustion-confirmation` | [archive](../../../openspec/changes/archive/2026-08-28-runtime-viewport-exhaustion-confirmation/) |
| `2026-08-30-dsh-uniflow-profile-adapter` | [archive](../../../openspec/changes/archive/2026-08-30-dsh-uniflow-profile-adapter/) |
| `2026-08-30-dsh-uniflow-run-scoped-operational-state` | [archive](../../../openspec/changes/archive/2026-08-30-dsh-uniflow-run-scoped-operational-state/) |
| `2026-08-30-fast-semantic-container-identity-baseline` | [archive](../../../openspec/changes/archive/2026-08-30-fast-semantic-container-identity-baseline/) |
| `2026-08-30-greenfield-agent-runtime` | [archive](../../../openspec/changes/archive/2026-08-30-greenfield-agent-runtime/) |
| `2026-08-30-observability-emission-expansion` | [archive](../../../openspec/changes/archive/2026-08-30-observability-emission-expansion/) |
| `2026-08-30-observability-evidence-anchors` | [archive](../../../openspec/changes/archive/2026-08-30-observability-evidence-anchors/) |
| `2026-08-30-observability-trajectory-timing` | [archive](../../../openspec/changes/archive/2026-08-30-observability-trajectory-timing/) |
| `2026-08-30-perception-navigation-row-composition-repair` | [archive](../../../openspec/changes/archive/2026-08-30-perception-navigation-row-composition-repair/) |
| `2026-08-30-profile-source-content-pinning` | [archive](../../../openspec/changes/archive/2026-08-30-profile-source-content-pinning/) |
| `2026-08-30-runtime-agent-decision-adaptation` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-agent-decision-adaptation/) |
| `2026-08-30-runtime-agent-directive-capability` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-agent-directive-capability/) |
| `2026-08-30-runtime-agent-plan-hypothesis` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-agent-plan-hypothesis/) |
| `2026-08-30-runtime-agent-reconciliation-decision` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-agent-reconciliation-decision/) |
| `2026-08-30-runtime-agent-strategy-execution-loop` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-agent-strategy-execution-loop/) |
| `2026-08-30-runtime-debug-artifact-out` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-artifact-out/) |
| `2026-08-30-runtime-debug-p1a-summarize-occurrence` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p1a-summarize-occurrence/) |
| `2026-08-30-runtime-debug-p1b-causal-diff-projection` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p1b-causal-diff-projection/) |
| `2026-08-30-runtime-debug-p1c-asset-index` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p1c-asset-index/) |
| `2026-08-30-runtime-debug-p1d-packet-generator` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p1d-packet-generator/) |
| `2026-08-30-runtime-debug-p2a-run-compare` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p2a-run-compare/) |
| `2026-08-30-runtime-debug-p2b-trace-diff` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p2b-trace-diff/) |
| `2026-08-30-runtime-debug-p2c-terminal-chain` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p2c-terminal-chain/) |
| `2026-08-30-runtime-debug-p2d-execution-tree` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p2d-execution-tree/) |
| `2026-08-30-runtime-debug-p3-tui` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p3-tui/) |
| `2026-08-30-runtime-debug-p4a-replay-facts` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p4a-replay-facts/) |
| `2026-08-30-runtime-debug-p4b-replay-projection` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p4b-replay-projection/) |
| `2026-08-30-runtime-debug-p4c-minimize` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p4c-minimize/) |
| `2026-08-30-runtime-debug-p5-diagnosis-workflow` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debug-p5-diagnosis-workflow/) |
| `2026-08-30-runtime-debugging-toolchain` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-debugging-toolchain/) |
| `2026-08-30-runtime-scenario-knowledge-boundary-cleanup` | [archive](../../../openspec/changes/archive/2026-08-30-runtime-scenario-knowledge-boundary-cleanup/) |
| `2026-08-30-trace-span-read-model` | [archive](../../../openspec/changes/archive/2026-08-30-trace-span-read-model/) |
| `2026-08-30-uniagent-runtimeagent-strategy-contract` | [archive](../../../openspec/changes/archive/2026-08-30-uniagent-runtimeagent-strategy-contract/) |
| `2026-08-30-uniflow-required-skill-propagation` | [archive](../../../openspec/changes/archive/2026-08-30-uniflow-required-skill-propagation/) |
| `2026-08-30-universal-ai-coder-protocol-migration` | [archive](../../../openspec/changes/archive/2026-08-30-universal-ai-coder-protocol-migration/) |
| `physical-settings-to-wifi-multi-level-traversal` | [archive](../../../openspec/changes/archive/physical-settings-to-wifi-multi-level-traversal/) |
| `physical-wifi-off-to-on-minimum-semantic-loop` | [archive](../../../openspec/changes/archive/physical-wifi-off-to-on-minimum-semantic-loop/) |
| `switch-state-reading` | [archive](../../../openspec/changes/archive/switch-state-reading/) |

## Count check

| Lifecycle view | Count |
|---|---:|
| Current Active | 12 |
| Historical Archived | 81 |
