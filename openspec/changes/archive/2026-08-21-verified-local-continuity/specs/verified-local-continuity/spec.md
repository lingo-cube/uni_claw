# Spec: verified-local-continuity

> APPLY spec for the bounded VerifiedLocalContinuity repair
> (SCROLLED_CONTAINER_IDENTITY_DRIFT). Frozen semantics: AbsoluteResolver == null
> means absolute recognition unavailable, NOT identity contradicted.

## ADDED Requirements

### Requirement: VerifiedLocalContinuity fallback

When the absolute page resolver returns null for a fresh Observation after a
same-Container action, the Agent MAY preserve the previous semantic page ONLY when
fresh continuity evidence independently verifies same-Container continuity. The
resulting page identity MUST be recorded as Source=VERIFIED_LOCAL_CONTINUITY.

#### Scenario: same-page scrolled observation is preserved

Given a scrollable page whose title scrolled out of view after a same-Container
action, and fresh continuity evidence (compatible foreground, fresh structural
evidence, no other-page match, no contradiction),
When the absolute resolver returns null,
Then the previous semantic page is preserved via VERIFIED_LOCAL_CONTINUITY.

#### Scenario: resolver-null never implies previous page

Given the absolute resolver returns null,
When no fresh continuity evidence independently verifies same-Container continuity,
Then the page remains unknown and existing fail-closed behavior is preserved —
never `resolver == null → previousPage`.

### Requirement: Continuity predicate conditions

The VerifiedLocalContinuity predicate MUST require ALL applicable conditions:
(1) previous SemanticPage verified; (2) foreground compatible; (3) same-Container
action scope (narrowest: ScrollForward / SetSwitch); (4) fresh Observation contains
structurally compatible evidence (row/control elements, not bare text); (5) no
other known SemanticPage positively matches; (6) no navigation/transition evidence;
(7) no fresh contradictory evidence.

#### Scenario: insufficient evidence fails closed

Given a fresh Observation containing only a bare text fragment (no row/control
structure),
When continuity is evaluated,
Then the page remains unknown and fail-closed is preserved.

#### Scenario: other-page match rejects continuity

Given a fresh Observation that positively matches another known SemanticPage,
When continuity is evaluated,
Then continuity is rejected and the existing navigation/reconciliation path decides.

### Requirement: Absolute recognition precedence

When the fresh Observation positively resolves to a SemanticPage, that result MUST
be used. VerifiedContinuity is ONLY a fallback for absolute resolver == null and
MUST NOT override a positive match to another page.

#### Scenario: positive absolute resolution wins

Given a fresh Observation that resolves to a known SemanticPage,
When the semantic loop reconciles,
Then the absolute result is used and VerifiedContinuity is not consulted.

### Requirement: Fresh binding/state freeze

Binding, state reconciliation, SwitchState evidence, GoalEvidence semantics,
post-action settle, and Assistance/L1/L2 MUST remain unchanged. Binding and state
evidence MUST be freshly resolved from the new Observation in the verified path.

#### Scenario: fresh evidence still used

Given a verified-continuity acceptance,
When the Container refreshes,
Then binding and state beliefs are computed from the new Observation (never stale).

## MODIFIED Requirements

None. This change modifies no existing spec or implementation semantics beyond the
bounded continuity fallback (existing fail-closed unknown behavior remains when
continuity evidence is insufficient).
