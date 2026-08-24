# Design: verified-local-continuity

> APPLY design reconstructed from the approved proposal, frozen spec, and
> executed task record. This design adds no requirements beyond those artifacts.

## 1. Scope and existing behavior

The absolute page resolver remains authoritative whenever it positively resolves
a page. A null result means that absolute recognition is unavailable; it is not
itself a semantic contradiction. Existing fail-closed behavior remains the
default, and the fallback is limited to a fresh observation following a
same-Container `ScrollForward` or `SetSwitch` action.

## 2. Verified continuity decision

The Agent evaluates `IsVerifiedLocalContinuity` only after absolute resolution
returns null. The predicate requires all applicable conditions from the spec:

1. the previous semantic page was verified;
2. the foreground is compatible;
3. the action scope is the same Container and is limited to `ScrollForward` or
   `SetSwitch`;
4. the fresh observation has structurally compatible row/control evidence, not
   bare text;
5. no other known semantic page positively matches;
6. no navigation or transition evidence exists; and
7. no fresh contradictory evidence exists.

If every condition holds, the previous page is carried forward with source
`VERIFIED_LOCAL_CONTINUITY`. Otherwise the page remains unknown and the existing
reconciliation path decides. A positive absolute match always wins and bypasses
this predicate.

## 3. Runtime integration

The post-action and strict post-scroll paths use the same bounded fallback:

1. observe and settle as already specified;
2. attempt absolute page resolution;
3. on a null result, evaluate the continuity predicate;
4. when accepted, mechanically verify same-Container continuity and record the
   viewport observation for scroll acceptances;
5. refresh the semantic snapshot using verified-continuity belief evaluation;
6. freshly resolve binding, state, SwitchState evidence, and GoalEvidence from
   the new observation; then continue the same goal on the same Container.

The default snapshot refresh path remains unchanged when the boolean
`verifiedLocalContinuity` flag is false. Continuity acceptance does not write
binding, state, or goal beliefs from stale data and does not alter Assistance,
L1, or L2 behavior.

## 4. Container behavior

`TryAcceptVerifiedContinuity` performs the mechanical same-Container check,
including strict sequence advancement and compatible foreground. The verified
belief evaluation treats local identity as supported rather than contradicted.
The original page-belief evaluation remains available unchanged for
backward-compatible non-verified refreshes.

## 5. Verification coverage

The executed task record is the verification record: T1–T15 and T8b cover
positive absolute precedence, repeated scrolls, SetSwitch, insufficient/stale
evidence, foreground and other-page rejection, navigation and popup rejection,
fresh binding/state, and unchanged normal recognition. The real-device corpus
rerun eliminated the false semantic contradiction (6/24 to 0/24) while retaining
truthful `BindingUnresolved` behavior and WiFi navigation results.

## 6. Non-goals and safety boundary

This design does not redesign perception, alter binding/state semantics, add a
public taxonomy, introduce temporal inference, couple Assistance/L1/L2, or
generalize the fallback beyond the narrow same-Container action scope. Resolver
null alone can never imply the previous page.
