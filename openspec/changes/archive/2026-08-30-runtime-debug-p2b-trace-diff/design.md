# Design — runtime-debug-p2b-trace-diff

## Context

P1b proved per-packet causal-tree projection; P2a proved paired-bundle structural diff. P2b compares two packets' chains — the packet-level Good/Bad pair analysis surface — reusing only stored facts.

## Goals / Non-Goals

Goals: mechanical chain diff (stage presence/status/refs axes), first mechanically changed stage, stored LastGood/FirstBad projection, fail-closed.

Non-Goals: semantic first-change inference; cross-chain alignment beyond stage-key union; packet-vs-bundle mixing; timing diff.

## Decisions

### D1 — Stage alignment by chain order with union
**Decision:** stage order follows the good chain, then bad-only stages appended in bad order. Status equality and ref-set equality define the axes.
**Why:** deterministic and faithful to each packet's stored order; no re-sorting by status.

### D2 — Mechanical first change is explicit
**Decision:** `firstMechanicallyChangedStage` = first stage with any axis change; the note explicitly declares that FIRST_SEMANTICALLY_RELEVANT_CHANGE is not inferred.
**Why:** structural-facts-first; the Agent/Skill owns semantic first-divergence judgment over this mechanical scaffold.

## Risks / Trade-offs

- [Different case packets compared] → allowed (mechanical), but CaseId differences are surfaced in good/bad blocks for the user to notice.

## Migration Plan

None — additive command.

## Open Questions

None that would change the contract.
