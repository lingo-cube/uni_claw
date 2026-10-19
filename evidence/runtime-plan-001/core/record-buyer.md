# Record-set buyer follow-up

## Scope and result

`RecordBuyer_UsesRecordStateProjectionAndCurrentGuardThroughSamePlanDriver` in `tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverPlanTests.cs` exercises a non-page record collection through the public `KernelRunDriver` seam. It uses no Settings-specific driver behavior. The final focused test group and Kernel suite both pass; see `focused-green-final.log` and `kernel-full-suite-followup-final.log`.

## Evidence path

- The source projection exposes R17 as Ready and R22 as locally Blocked in a stable record-ledger scope. R22 carries owner evidence `owner-proof:R22:lease-locked`, and the Plan opts into `ContinueOnBlocked`.
- R17 depends on owner evidence. The Plan explicitly observes `record:R17.supplement`; the observation advances the fixture from `EvidencePending` to `EvidenceCollected`.
- The candidate-bound action targets semantic role `record` and descriptor `R17`. Its step guard checks current record facts before dispatch.
- The effect callback records delivery only. A separate post-action owner observation supplies `record:R17.status=Verified`; the ordinary owner claim obligation uses that status directly, without UI checked-state or entity-scope semantics.
- The attempt links source/guard evidence to its sole effect receipt, then links independent post-action evidence to verification. The completion reference uses the exact verified plan-attempt anchor.
- Initial UI observation time matches `UIWorldDoubles.T0`, so the generated evidence/container identity matches the probe's seed identity. Later observations remain after dispatch.

## Core regression coverage

The same focused set checks that a body Observe already satisfied advances and finalizes the current candidate attempt without a receipt; gate rejection updates rather than duplicates the Started attempt; a plain Act guard rejection does not leak a synthetic Plan history into a later Plan; focused-control retries keep one attempt and reconcile exhaustion or guard rejection; and repeated ForEach iterations cannot satisfy a linear `step:N.M` completion anchor. An exact verified `plan-attempt:` anchor remains accepted.

The root-null case was reproduced through public owner-conflict inputs and passed in `root-null-attempt-4.log`; earlier invalid fixture attempts are retained as `root-null-attempt-2.log` and `root-null-attempt-3.log`. The regression and its test-only association helper remain in the focused suite.

## Verification

- Focused Plan-driver tests: 37 passed, 0 failed.
- Full `UniClaw.Kernel.Tests`: 936 passed, 0 failed.
- `git diff --check`: passed.
