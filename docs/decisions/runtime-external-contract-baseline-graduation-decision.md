# runtime-external-contract-baseline — Graduation Decision

> Status: GRADUATED | Scope: documentation-only external contract baseline.

## Buyer

Future external clients need a stable vocabulary and boundary for Goal/Data while knowing which Assistance, Guidance, and Execution Handoff planes are not implemented.

## Exact claim boundary

The baseline documents five planes, versioning, correlation/world-version primitives, authority clauses, and maps the existing `run.start`, read-only methods, snapshots, events, and evidence. It introduces no code or new wire format for deferred planes.

## Validation evidence

`openspec/changes/runtime-external-contract-baseline/tasks.md` records all documentation slices complete, strict OpenSpec validation, consistency, and gap-analysis cross-check.

## Falsifier result

F1–F9 are recorded passed: zero code change, no DSH types in Runtime, unchanged frozen semantics, and explicit deferred-plane boundaries.

## Deferred scope

Assistance, Guidance, Execution Handoff, TaskSpec/AgentProfile, and future message schemas require separate gates and real buyers.

## Final lifecycle conclusion

The documentation baseline is graduated; it is a contract boundary and does not authorize implementation of deferred planes.
