# CORE-003 — Core 对象与最小语义关系

Status: `READY FOR IMPLEMENTATION`
Triage: `ready-for-agent`
Publication: repository Change State（本仓库未配置外部 issue tracker）

## Problem Statement

当前 `UniClaw.Core` 已有第一条 tracer 和若干候选记录，但对象边界、最小关系和
核心不变量还没有作为一个独立的 Core 对象规格整理完成。如果现在直接对齐旧
Kernel/UI 模型，容易让旧类签名、字段或视觉结构反过来决定 Core。

本 Change 需要先建立能够跨手机、浏览器、机器人和仿真复用的最小语义对象集，
并用纯 Core 场景证明它们的职责和边界。它不负责迁移或重构旧模型。

## Solution

先建立以下候选 Core 语义对象及关系：

```text
Core = Specification + World + Effect

Specification: Clause
World: Segment ──contains──> Slice
       Evidence, Claim, Event
Effect: Effect ──contains──> Attempt ──uses──> TargetBinding
```

当前最小语义基线保留六类核心记录：Clause、Segment、Evidence、Claim、Event、
Effect。Slice 是 Segment 内的局部观察切片；Attempt 和 TargetBinding 是 Effect
内部的执行语义。六类记录是语义基线，不要求对应六个基类、数据库对象或独立
程序集。

Event 必须能表达有来源的发生、时间、参与者、数量、同一性和因果判断；独立 Event
类型是否必要，必须由表达失败或演化失败的反例决定，不能先验冻结。

## User Stories

1. As a Core consumer, I want Clause to express requirements, permissions, constraints, and criteria, so that observation never creates authorization.
2. As a World realization, I want Segment to provide a continuous reference, so that a record number is not mistaken for real-world identity.
3. As a mobile traversal realization, I want one Segment to contain multiple historical Slices, so that scrolling creates history instead of overwriting it.
4. As an observation consumer, I want Slices to overlap, contain, or repeat coverage, so that incomplete observation is represented honestly.
5. As an evidence producer, I want Evidence to preserve source, time, processing chain, and context, so that provenance can be reconstructed.
6. As a World Model, I want Claim to retain support, conflict, correction, and uncertainty, so that judgment is not treated as reality.
7. As an event consumer, I want occurrence and causal semantics expressible without an automatic permission side effect, so that an observed event remains distinct from a rule.
8. As an Effect planner, I want logical operations separate from Attempts, so that retries, delays, partial delivery, and compensation remain distinguishable.
9. As an executor, I want each Attempt to refer to the binding used for that attempt, so that actual delivery is not confused with the logical Effect.
10. As a binding consumer, I want TargetBinding to retain target reference, locator, fixed basis, and validity conditions, so that historical targets do not follow latest resolution.
11. As a verification consumer, I want delivery outcome separate from World result, so that completion of a send is not proof that the target changed.
12. As a Core maintainer, I want unknown, stale, ambiguous, unauthorized, and unverified states to remain distinct, so that missing knowledge is not upgraded by convenience.
13. As a history consumer, I want new observations and new evidence to append or revise explicitly, so that old supporting material is never silently rewritten.
14. As a realization author, I want UI, robot, browser, device, and perception payloads to remain opaque at the Core boundary, so that Core stays domain-neutral.
15. As a validation owner, I want a deterministic phone scroll-and-click tracer to exercise the complete relation chain, so that the object set is tested by behavior rather than by class shape.
16. As a migration owner, I want legacy-model alignment explicitly deferred, so that old classes cannot silently become the Core specification.

## Implementation Decisions

- Implement the object set as a candidate semantic protocol inside `UniClaw.Core`; do not
  copy legacy class signatures or declare a final public API.
- Keep Core free of UI, device SDK, browser, robot, perception, Runtime, Trace, Harness,
  storage, and transport dependencies.
- Model Segment continuity separately from Slice observation scope. A Slice may be
  partial, overlapping, contained, or repeated; a later Slice never mutates an earlier
  Slice.
- Keep Evidence as provenance-bearing input and Claim as a judgment that cites Evidence.
  Evidence admission or storage does not automatically accept a Claim.
- Preserve Event occurrence semantics, but defer the decision for an independent Event
  type until a concrete expression/evolution counterexample requires it.
- Keep Effect, Attempt, and TargetBinding semantically separate. A binding records the
  target, locating material, fixed basis, and validity conditions; it may be stale or
  unauthorized without being silently replaced.
- Keep delivery state and World verification separate. Unknown delivery remains unknown;
  it does not become success or failure without evidence.
- Use value-oriented, deterministic rules for the first Core slice. Do not introduce a
  generic freshness, trust, confidence, correlation, or version field merely because a
  field is convenient for one realization.
- The highest test seam is the Core semantic test assembly. A single realization seam may
  be used only to provide a deterministic tracer input; it must not define Core meaning.
- Legacy alignment is a subsequent Change. Later work may promote, split, merge, replace,
  compose, inherit, reference, adapt, or delete old models according to responsibility;
  no one-to-one mapping is implied by this specification.

## Testing Decisions

- Tests assert externally observable semantic behavior and authority boundaries, not
  record layout, constructor shape, serialization, or one-to-one legacy mappings.
- The primary vertical test constructs a Segment, appends multiple Slices, records
  Evidence, derives Claim and Event semantics, creates an Effect, fixes a TargetBinding,
  records an Attempt, and adds post-action Evidence.
- Tests must prove: old Slice history is unchanged; binding basis does not follow latest;
  Attempt time is independent of observation time; Unknown is not upgraded; delivery
  does not create a World result; and stale/ambiguous/unauthorized bindings fail closed.
- Event tests first cover occurrence, source, time, participants, count, identity and
  causal judgment. They may keep the representation composite until an independent type
  is required by a failing expression or evolution test.
- A deletion/evolution check must compare “update then use Core” with “use Core then
  update” for the same semantic history; both paths must preserve the same necessary
  distinctions.
- Existing Core tracer tests are prior art. Kernel projection and legacy alignment tests
  are out of scope for this Change and must not be used to claim Core object completion.
- Passing tests establish a candidate Core object baseline only; they do not prove strict
  minimality, production readiness, or completion of source migration.

## Out of Scope

- Aligning, renaming, migrating, or deleting existing Kernel/UI/legacy models.
- Building Browser, Robot, Mobile, Simulation, Runtime, Harness, storage, or transport
  implementations.
- Freezing final field lists, inheritance trees, namespaces, package names, serialization,
  database layout, or wire protocols.
- Making observation create permissions or turning claims, events, receipts, or bindings
  into a second fact authority.
- Selecting algorithms, perception providers, locator formats, or device protocols.
- Declaring strict mathematical minimality from the first tracer alone.

## Further Notes

This Change follows the locked Core semantic baseline and the vNext.1 design/alignment
materials. The next Change, after this object baseline is tested, will perform a separate
alignment pass against actual models and scenarios. That pass must be driven by concrete
responsibility and counterexamples, not by the current class hierarchy.

The repository has no configured external issue tracker; this `spec.md` and its Change
State are the durable publication and handoff surface.
