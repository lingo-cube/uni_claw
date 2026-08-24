# Current Gates

DocumentType: `CURRENT_STATE_PROJECTION`
Authority: `NONE`
GeneratedProjection: `true`
GenerationRule: `openspec list --json` plus direct `openspec/changes/*/proposal.md` membership check
GeneratedAt: `2026-08-24`
ProjectionState: `CURRENT`
ActiveChangeCount: `17`
ArchivedChangeCount: `42`

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

## Generated Active Change Membership — 17

| Change | Source reference |
|---|---|
| `runtime-exploration-ledger-and-depth-control` | [proposal](../../../openspec/changes/runtime-exploration-ledger-and-depth-control/proposal.md) · [tasks](../../../openspec/changes/runtime-exploration-ledger-and-depth-control/tasks.md) |
| `dsh-uniflow-profile-adapter` | [proposal](../../../openspec/changes/dsh-uniflow-profile-adapter/proposal.md) · [tasks](../../../openspec/changes/dsh-uniflow-profile-adapter/tasks.md) |
| `fast-semantic-container-identity-baseline` | [proposal](../../../openspec/changes/fast-semantic-container-identity-baseline/proposal.md) · [tasks](../../../openspec/changes/fast-semantic-container-identity-baseline/tasks.md) |
| `greenfield-agent-runtime` | [proposal](../../../openspec/changes/greenfield-agent-runtime/proposal.md) · [tasks](../../../openspec/changes/greenfield-agent-runtime/tasks.md) |
| `runtime-agent-decision-adaptation` | [proposal](../../../openspec/changes/runtime-agent-decision-adaptation/proposal.md) · [tasks](../../../openspec/changes/runtime-agent-decision-adaptation/tasks.md) |
| `runtime-agent-directive-capability` | [proposal](../../../openspec/changes/runtime-agent-directive-capability/proposal.md) · [tasks](../../../openspec/changes/runtime-agent-directive-capability/tasks.md) |
| `runtime-agent-plan-hypothesis` | [proposal](../../../openspec/changes/runtime-agent-plan-hypothesis/proposal.md) · [tasks](../../../openspec/changes/runtime-agent-plan-hypothesis/tasks.md) |
| `runtime-agent-pre-terminal-cycle-contract` | [proposal](../../../openspec/changes/runtime-agent-pre-terminal-cycle-contract/proposal.md) · [tasks](../../../openspec/changes/runtime-agent-pre-terminal-cycle-contract/tasks.md) |
| `runtime-agent-reconciliation-decision` | [proposal](../../../openspec/changes/runtime-agent-reconciliation-decision/proposal.md) · [tasks](../../../openspec/changes/runtime-agent-reconciliation-decision/tasks.md) |
| `runtime-agent-strategy-execution-loop` | [proposal](../../../openspec/changes/runtime-agent-strategy-execution-loop/proposal.md) · [tasks](../../../openspec/changes/runtime-agent-strategy-execution-loop/tasks.md) |
| `runtime-external-semantic-capability-boundary` | [proposal](../../../openspec/changes/runtime-external-semantic-capability-boundary/proposal.md) · [tasks](../../../openspec/changes/runtime-external-semantic-capability-boundary/tasks.md) |
| `runtime-scenario-knowledge-boundary-cleanup` | [proposal](../../../openspec/changes/runtime-scenario-knowledge-boundary-cleanup/proposal.md) · [tasks](../../../openspec/changes/runtime-scenario-knowledge-boundary-cleanup/tasks.md) |
| `semantic-perception-contract-baseline` | [proposal](../../../openspec/changes/semantic-perception-contract-baseline/proposal.md) · [tasks](../../../openspec/changes/semantic-perception-contract-baseline/tasks.md) |
| `semantic-perception-layer-baseline` | [proposal](../../../openspec/changes/semantic-perception-layer-baseline/proposal.md) · [tasks](../../../openspec/changes/semantic-perception-layer-baseline/tasks.md) |
| `trace-capture-scenario-catalog-foundation` | [proposal](../../../openspec/changes/trace-capture-scenario-catalog-foundation/proposal.md) · [tasks](../../../openspec/changes/trace-capture-scenario-catalog-foundation/tasks.md) |
| `trace-span-read-model` | [proposal](../../../openspec/changes/trace-span-read-model/proposal.md) · [tasks](../../../openspec/changes/trace-span-read-model/tasks.md) |
| `uniagent-runtimeagent-strategy-contract` | [proposal](../../../openspec/changes/uniagent-runtimeagent-strategy-contract/proposal.md) · [tasks](../../../openspec/changes/uniagent-runtimeagent-strategy-contract/tasks.md) |

Task progress is read from each linked `tasks.md`; this projection does not
maintain a second aggregate completion/graduation status.

## Gate Annotations

- `runtime-exploration-ledger-and-depth-control` is graduated per its
  [graduation decision](../../decisions/runtime-exploration-ledger-and-depth-control-graduation-decision.md)
  (2026-08-25, independent evidence-verified after WI-RELC-003 remediation of the
  real-path fail-closed classification and revisit-coverage fusion gaps). The
  earlier "proposal only" apply-not-authorized state is superseded (apply was
  human-authorized at tasks 1.1/1.2 on 2026-08-24). Archive is a separate pending
  lifecycle operation; the change remains listed above until archived.
- `uniagent-runtimeagent-strategy-contract` is graduated per its
  [graduation decision](../../decisions/uniagent-runtimeagent-strategy-contract-graduation-decision.md)
  (2026-08-24, human-authorized verification path; six forbidden-edge proofs verified).
  Archive is a separate pending lifecycle operation; the change remains listed above
  until archived.
- `semantic-perception-layer-baseline` remains `APPLY_NOT_AUTHORIZED` by its
  [decision](../../decisions/semantic-perception-layer-baseline.md).
- `trace-capture-scenario-catalog-foundation` retains two open validation tasks;
  see its [implementation review](trace-capture-scenario-catalog-foundation-implementation-review.md)
  and [architecture gate](../../decisions/trace-capture-scenario-catalog-architecture-gate.md).

## Historical Archived — 41

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
| `physical-settings-to-wifi-multi-level-traversal` | [archive](../../../openspec/changes/archive/physical-settings-to-wifi-multi-level-traversal/) |
| `physical-wifi-off-to-on-minimum-semantic-loop` | [archive](../../../openspec/changes/archive/physical-wifi-off-to-on-minimum-semantic-loop/) |
| `switch-state-reading` | [archive](../../../openspec/changes/archive/switch-state-reading/) |
| `2026-08-21-agent-concept-model-v1-alignment` | [archive](../../../openspec/changes/archive/2026-08-21-agent-concept-model-v1-alignment/) |
| `2026-08-21-post-action-state-settle` | [archive](../../../openspec/changes/archive/2026-08-21-post-action-state-settle/) |
| `2026-08-21-verified-local-continuity` | [archive](../../../openspec/changes/archive/2026-08-21-verified-local-continuity/) |
| `2026-08-21-runtime-assistance-seam` | [archive](../../../openspec/changes/archive/2026-08-21-runtime-assistance-seam/) |
| `2026-08-21-dsh-assistance-provider-adapter` | [archive](../../../openspec/changes/archive/2026-08-21-dsh-assistance-provider-adapter/) |
| `2026-08-21-dsh-runtime-agent-subagent-run-entry` | [archive](../../../openspec/changes/archive/2026-08-21-dsh-runtime-agent-subagent-run-entry/) |
| `2026-08-21-runtime-external-contract-baseline` | [archive](../../../openspec/changes/archive/2026-08-21-runtime-external-contract-baseline/) |
| `2026-08-21-vision-runtime-bootstrap` | [archive](../../../openspec/changes/archive/2026-08-21-vision-runtime-bootstrap/) |
| `2026-08-21-uniclaw-driverhost-production-server-mode` | [archive](../../../openspec/changes/archive/2026-08-21-uniclaw-driverhost-production-server-mode/) |
| `2026-08-21-uniagent-decision-goal-evaluation-minimum-contract` | [archive](../../../openspec/changes/archive/2026-08-21-uniagent-decision-goal-evaluation-minimum-contract/) |

## Count check

| Lifecycle view | Count |
|---|---:|
| Current Active | 17 |
| Historical Archived | 41 |
