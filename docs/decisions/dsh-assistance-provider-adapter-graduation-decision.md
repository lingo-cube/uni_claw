# dsh-assistance-provider-adapter — Graduation Decision

> Status: GRADUATED | Scope: cross-process assistance adapter and deterministic consumer.

## Buyer

The DSH/DriverHost boundary needs bounded assistance transport without giving DSH Runtime or GoalEvidence authority.

## Exact claim boundary

The adapter uses the existing DSH→DriverHost direction with pending/poll/resolve, validates request identity, world version and whitelist, bounds capacity and timeout, and never writes Runtime state. Provider absence remains fail-closed.

## Validation evidence

`openspec/changes/dsh-assistance-provider-adapter/tasks.md` records 10/10 provider tests, 8/8 bridge tests, 1/1 real cross-process model-free E2E, and 7/7 seam regression.

## Falsifier result

F1–F12 are recorded passed, including additive-only wire compatibility, no reverse connection, no model references in Runtime, and no intelligence policy in the adapter.

## Deferred scope

Real model consumer attachment and broader assistance semantics are separate work; this change does not authorize them.

## Final lifecycle conclusion

The bounded adapter path is graduated. Assistance remains optional advice, not truth, authorization, or goal completion.
