# Spec: dsh-assistance-provider-adapter

## Purpose

Define the harness-side Assistance provider adapter for the external contract boundary.

## Requirements

### Requirement: Cross-process direction (pending queue + poll + resolve)

The adapter MUST resolve the Runtime→DSH synchronous consult over the EXISTING
DSH→DriverHost connection direction using a bounded pending registry, a
`assistance.pending` poll, and an `assistance.resolve` submit. NO reverse
connection (DriverHost→plugin listener) MAY be introduced.

#### Scenario: consult enqueues and awaits

Given an Agent consult at an adjudication point,
When `AssistanceWireProvider.ConsultAsync` is invoked,
Then the request is enqueued in the bounded pending registry and the call awaits a
bounded response (or timeout).

#### Scenario: poll reuses the existing connection direction

Given the wire surface,
When `assistance.pending` is invoked by the plugin,
Then it returns the pending request digests over the existing DSH→DriverHost
connection — no second connection direction exists.

### Requirement: Additive wire methods

The adapter MUST add exactly two wire methods (`assistance.pending`,
`assistance.resolve`) additively; the frozen 8 read-only methods and `run.start`
keep exact semantics.

#### Scenario: frozen semantics preserved

Given the DriverHost dispatch table,
When the frozen methods are invoked,
Then they behave exactly as before (R10 precedent; additive-only).

### Requirement: Resolve writes only the pending reply

`assistance.resolve` MUST complete only the pending-request reply slot — never
belief, binding, Container, state, or GoalEvidence.

#### Scenario: resolve does not touch kernel state

Given a resolved advice,
When the DriverHost consumer completes the awaited consult,
Then no Kernel state is written; the Agent applies the advice through its own
deterministic mechanisms (I-3).

### Requirement: Advice validation

The resolve path MUST validate requestId echo and world-version match against the
pending entry, and MUST normalize `recommendation` against the Agent's accepted
whitelist (`re-observe` / `rebind` / `dismiss-obstruction` / null).

#### Scenario: uncorrelated or stale resolve is rejected

Given a resolve with mismatched requestId or worldVersion,
When it is processed,
Then it returns `resolved: false` and the request stays pending until timeout.

#### Scenario: unknown recommendation becomes abandon

Given a resolve whose recommendation is not in the whitelist,
When it is processed,
Then the advice is normalized to null (abandon) and the Agent fails closed.

### Requirement: Bounded consult

The registry MUST be capacity-bounded and each consult MUST have a timeout;
overflow or timeout MUST yield null advice (Agent fail-closed) — never a hang,
never fabricated progress.

#### Scenario: timeout fails closed

Given a consult whose pending entry times out,
When the await completes,
Then `ConsultAsync` returns null and the Agent fails closed exactly as with no
provider.

### Requirement: Harness intelligence confined to the plugin

The DSH plugin MUST own advice generation through an **optional** Harness
intelligence consumer; the DriverHost and Runtime MUST NOT reference a model/LLM.

#### Scenario: no model reference outside the plugin

Given the change's runtime surface,
When the DriverHost and Runtime code are inspected,
Then they reference no model/LLM/VLM (Guard 2; F2).

#### Scenario: consumer is optional and replaceable

Given the plugin's assistance service,
When no intelligence consumer is registered,
Then the service either uses a deterministic fallback or abandons the request
(Agent fails closed) — activation never depends on an inference service.

### Requirement: Bridge provider-agnostic (adapter is not the decision layer)

The AssistanceBridge MUST be a provider-agnostic protocol translator: it MUST NOT
own semantic decision policy, MUST NOT hard-code an LLM as the only intelligence
mechanism, MUST NOT become an intelligence router, and MUST NOT implement Runtime
recovery/planning semantics. Intelligence selection/composition belongs to the
Harness consumer.

#### Scenario: bridge never calls the model directly

Given the bridge implementation,
When its source is inspected,
Then it references no llm/model package and no `ctx.get('llm')` call — it submits
the translated request through a consumer port to an AVAILABLE Harness consumer.

#### Scenario: consumer port is swappable

Given the bridge with a registered consumer,
When the consumer is replaced by a stub/deterministic consumer,
Then the bridge resolves the consult identically (provider-agnostic by contract).

### Requirement: First APPLY is model-free

The first APPLY MUST prove the full cross-process path (Runtime →
AssistanceWireProvider → pending wire → AssistanceBridge → consumer → resolve →
Runtime → fresh world verification) with a fake/deterministic consumer — without
any real model.

#### Scenario: cross-process path works without a model

Given the first APPLY test environment,
When the full path is exercised,
Then it completes with a fake/deterministic consumer and no real model is invoked
(a real Harness consumer is attached independently afterwards).

### Requirement: Capability-gap wire vocabulary

The `assistance.pending` digest MUST be capability-gap context (element digests,
belief state, world version) — NOT a model prompt.

#### Scenario: request is not a prompt

Given an `AssistanceRequestDto`,
When it is inspected,
Then it carries no prompt/instruction text; only the adjudication context digest.

### Requirement: Runtime untouched

This change MUST NOT modify `UniClaw.Runtime` (the seam already exists) or
introduce new RuntimeEvent kinds/emitters.

#### Scenario: zero runtime footprint

Given the change's artifact set,
When it is inspected,
Then no `src/UniClaw.Runtime` file is modified and no new event kind/emitter is
defined.
