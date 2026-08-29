# runtime-evidence-based-quiescence-admission

> Status: **IMPLEMENTED_PENDING_GRADUATION_REVIEW** — implementation complete; the
> change awaits independent graduation review. Phase 2.6 remains STOPPED.

Defines `EVIDENCE_BASED_QUIESCENCE_ADMISSION` and repairs the EXISTING post-scroll
stability gate (repair-in-place; no second loop). Real defect (source-verified):
`NavigationRowCenters` collapses same-frame duplicate signatures (`Dictionary.TryAdd`)
— the gate can confirm an ambiguous frame as the stable decision basis, after which
the normalizer correctly fails. Repair: ordered, multiplicity-preserving stability
evidence; per-index signature + drift correspondence; in-frame ambiguity ⇒ frame is
never confirmable (pending or budget-exhausted fail-closed, nothing admitted, no
action). RED basis: Gate Scenarios 1/5/7. All authority boundaries frozen
(AuthorityDelta NONE; no normalizer/perception/identity change; no other buyers). Principle 8: Terminal Supervisory Handoff — budget exhaustion stays fail-closed and reports through the EXISTING RunFailed Surface B chain (no new wire/EventKind/DTO/mid-Run transport); UniAgent consumes terminal results only.

Lifecycle: supersedes WITHDRAWN `runtime-viewport-exhaustion-confirmation`;
`unique-corroboration-admission` stays ABANDONED_AS_PRIMARY_FIX.
