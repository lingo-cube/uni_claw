# Human Clarification Ruling #1 — perception-operator-rule-framework (2026-08-27)

Verbatim ruling (Human):

```text
Decision:
SPEC_CLARIFICATION_REQUIRED_BEFORE_IMPLEMENTATION_GATE

ImplementationAuthorization:
NOT_YET_AUTHORIZED

RecommendedBatchAfterClarification:
S1 + S2 + S4

Deferred:
S3 — SEPARATE_HUMAN_GATE
S5 — SEPARATE_POST_S2_DECISION

AuthorityDelta:
NONE

ArchitectureDelta:
PROPOSED_PERCEPTION_INTERNAL_ONLY
NOT_APPLIED

Phase2_6_Reentry:
NOT_AUTHORIZED
```

## Required clarifications (all applied in this revision)

1. **Equal-specificity conflict definition** (spec): was self-contradictory (unconditional
   rejection vs. intersection-rule resolution). Now: a conflict exists ONLY between
   equal-specificity rules defining the same parameter differently whose selectors have a
   REACHABLE intersection that is not covered by a higher-specificity rule on that
   parameter; mutually exclusive selectors are NOT conflicts; conservative rejection
   (fail-closed) is allowed when provability fails. Four scenarios pin the semantics.
2. **S2 input freeze** (spec + tasks): `row-relation-head` inputs are frozen to RAW
   visual regions (uncombined detector boxes + OCR text blocks) and pairwise geometric
   relation candidates; it must NOT consume established row groups (no
   identify-rows-to-identify-rows circularity); text/XML/VLM must not fabricate row
   identity. S2 shortfall STOPS at the fail-closed boundary — S3 is not auto-entered.
3. **S5 deferral** (spec + tasks): minimum sample sizes, evidence intervals, and proposal
   producer are deferred design inputs; S5 is a separate post-S2 decision and does NOT
   gate Phase 2.6 re-entry.
4. **Stale wording** (design authority table): "树结构唯一路径" replaced by selector
   intersection analysis + deterministic conflict detection.

## Batch structure after clarification (as ruled)

S1 → S2 → S4, per-stage HARD gates (S1 zero-diff or stop; S2 v1n counterexample +
cross-UI regression or stop; S4 veto/downgrade only). S3 separate Human Gate.
Phase 2.6 re-enters only when S2 or an authorized S3 yields exactly one navigation
candidate per visual row on the regression frames.
