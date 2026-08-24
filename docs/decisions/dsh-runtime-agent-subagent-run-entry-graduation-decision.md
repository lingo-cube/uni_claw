# dsh-runtime-agent-subagent-run-entry — Graduation Decision

> Status: GRADUATED | Scope: additive `run.start` entry and execution coordinator.

## Buyer

DSH needs to submit a Runtime goal and observe an independently executing DriverHost-owned run.

## Exact claim boundary

`run.start` validates input, allocates the run in DriverHost, returns before completion, reuses existing observability, rejects invalid/busy requests, and invokes the existing Agent semantic entry. It adds no physical authority, model call, second result protocol, or Agent→DSH dependency.

## Validation evidence

`openspec/changes/dsh-runtime-agent-subagent-run-entry/tasks.md` records T1–T12 passing, including real wire/E2E acceptance, completed and failed observability, rejection, compatibility, and same-device exclusivity.

## Falsifier result

F1–F10 are recorded passed, including non-blocking acceptance and zero model calls.

## Deferred scope

Pause/resume/stop/abort controls, TaskSpec, IntelligenceSeam, and multi-agent semantics remain deferred.

## Final lifecycle conclusion

The bounded subagent/run entry is graduated as an additive control-plane capability; Runtime remains the execution authority.
