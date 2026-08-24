# Tasks: runtime-external-contract-baseline

> System of record for the documentation-only contract baseline gate. This gate
> creates the contract documents and validates them; no code is written.

## Slices

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — Contract design body: five-plane taxonomy (Goal/Data IMPLEMENTED;
      Assistance/Guidance/Execution Handoff DEFERRED) with direction, target
      messages, semantics, status
- [x] Slice 2 — Implemented-surface mapping appendix (run.start + 8 read-only
      methods + RunSnapshot + RuntimeEvent + EvidenceRef ↔ target messages)
- [x] Slice 3 — Versioning policy (additive-first, frozen 9-method set,
      backward-compat, deprecation; contract version vs wire version distinction)
- [x] Slice 4 — Correlation + world-version primitives (reuse
      RuntimeEvent.CorrelationId/EventId + Observation.SequenceNumber; staleness rule)
- [x] Slice 5 — Authority clauses + collaboration levels L0–L3 + guard citations
- [x] Slice 6 — Spec (R1–R10 requirements + scenarios; MODIFIED = none)
- [x] Validation — openspec validate --strict, check-consistency.sh, gap-analysis
      cross-check

## Falsifier mapping

- [x] F1 — zero code change (all files under openspec/changes/runtime-external-contract-baseline/)
- [x] F2 — no DSH/Cordis types introduced into Runtime (document-only; Runtime untouched)
- [x] F3 — deferred planes described as zero-implementation (verified token absence:
      no AssistanceRequest/GuidanceProposal/ExecutionYield/ExecutionReturn in src/ or
      dsh-plugin-uniclaw/src/)
- [x] F4 — frozen semantics unchanged (8 read-only + run.start semantics cited as-is)
- [x] F5 — Guidance ≠ Truth ≠ Authorization ≠ Goal completion (explicit clause)
- [x] F6 — DSH authority = MUST_BE_NO (physical/GoalEvidence/binding/belief)
- [x] F7 — no future design assumed (TaskSpec/AgentProfile/intelligence settings
      named as non-assumed; deferred)
- [x] F8 — repository-truth claims verified (all current-reality statements cite
      verified files/methods/tokens)
- [x] F9 — deferred planes have NO frozen wire format (boundaries only; no SHALL
      clauses on message fields)
