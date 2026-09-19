# CORE-001 — UniClaw Core 语义协议提取

Status: `LOCKED v0.1`  
Triage: `ready-for-agent`  
Publication: repository Change State（本仓库未配置外部 issue tracker）

## Problem Statement

当前实现将跨领域语义、UI World realization、Effect Runtime 和 Harness 混在一起。这样浏览器、手机、机器人和仿真无法依赖同一个小而稳定的基础协议；同时，UI 专用字段、当前定位、投递回执和运行状态容易被误认为现实事实。

需要提取一个 Core 语义协议，明确 Specification、World、Effect 的边界，并允许下层按语义进行删除、拆分、组合、替换、继承和引用。

## Solution

建立以下候选 Core 语义边界：

```text
Core = Specification + World + Effect

Specification: Clause
World: Segment, Slice, Evidence, Claim, Event
Effect: Effect, Attempt, TargetBinding
```

六种核心记录是语义基线，不等于六个独立类、数据库对象或基类。`Event` 的发生语义必须保留，但独立 Event 类型只有在删除与演化测试证明现有结构无法表达时才建立。

第一条验证路径是：手机滚动页面 → 多个 Slice → 元素 Claim → Effect → Attempt → TargetBinding → 点击后新 Evidence。浏览器、机器人和仿真都通过 realization 或 Adapter 依赖 Core；Core 不依赖 UI、设备 SDK、识别库或 Harness。

## User Stories

1. As a Core consumer, I want Clause to express requirements, permissions, constraints, and criteria, so that observation cannot create authorization.
2. As a World realization, I want Segment to provide a continuous reference across observations, so that record numbers are not mistaken for real identity.
3. As a mobile traversal realization, I want overlapping Slices for different viewports, so that scrolling does not overwrite historical observations.
4. As a binding consumer, I want the observation basis to be fixed or reconstructable, so that a later revision cannot silently change a historical target.
5. As an Evidence producer, I want source, time, processing chain, and context preserved, so that provenance remains inspectable.
6. As a World Model, I want Claims to retain uncertainty, conflict, correction, and supporting Evidence, so that judgment is not treated as reality.
7. As a causal-analysis consumer, I want occurrence, timing, participants, count, and causal judgment expressible, so that Event semantics are not lost merely because its independent type is unresolved.
8. As an Effect planner, I want logical operations separate from Attempts, so that retries, partial execution, timeout, and compensation remain distinguishable.
9. As an Effect executor, I want TargetBinding to capture the target, locator, fixed basis, and validity conditions for one Attempt, so that stale or ambiguous targets fail closed.
10. As a Runtime, I want delivery completion, delivery failure, unknown delivery, and World verification separate, so that a receipt cannot prove the result.
11. As a Browser adapter, I want DOM, accessibility, screenshot, and coordinate details below the Core seam, so that UI assumptions do not become cross-domain facts.
12. As a Robot adapter, I want pose, region, object, sensor, and actuator details below the Core seam, so that robots do not imitate UI classes.
13. As a Simulation adapter, I want deterministic World and Effect realizations, so that protocol behavior can be tested without creating a second product authority.
14. As a Core maintainer, I want pure semantic rules without IO, so that admission, relevance, binding validity, uncertainty, and conflict are not duplicated by each domain.
15. As a migration owner, I want old models classified by responsibility, so that mixed models can be split, combined, replaced, composed, inherited, referenced, or deleted.
16. As a migration owner, I want specializations to preserve shared meanings and constraints, so that inheritance cannot weaken history, evidence, uncertainty, or permission boundaries.
17. As a validation owner, I want the phone scroll-and-click path to exercise Segment, Slice, Evidence, Claim, Effect, Attempt, and TargetBinding, so that the seam is proven by behavior.
18. As a validation owner, I want a second non-UI realization to use different payloads and locators, so that cross-domain reuse is tested rather than assumed.
19. As a documentation owner, I want every rule to carry source, status, applicability, forbidden extrapolation, and scoped blocker, so that candidate ideas are not mistaken for architecture.
20. As an architecture owner, I want the Core boundary and semantic baseline locked only after review, so that implementation cannot decide the architecture retroactively.

## Implementation Decisions

- The first deliverable is a semantic protocol boundary, not a rename or relocation of the current Kernel project.
- Core contains Specification, World, Effect semantics and pure rules; it does not contain Runtime state machines, UI perception, device drivers, storage, transport, or Harness sessions.
- Six semantic records are retained: Clause, Segment, Evidence, Claim, Event, Effect. Slice is Segment-local; Attempt and TargetBinding are Effect-internal responsibilities.
- Historical bindings must use a fixed or reliably reconstructable basis. They do not follow latest or current canonical resolution automatically.
- Effect, Attempt, and TargetBinding remain separate. Unknown, stale, ambiguous, unauthorized, and unverified states remain distinct.
- Browser, Mobile, Robot, and Simulation are realizations or Adapters that depend on Core; Core does not depend on their concrete types.
- Old types are not mapped one-to-one. Responsibility decides whether a type is promoted, split, merged, replaced, composed, inherited, adapted, projected, retained in Runtime/Harness, or deleted.
- Inheritance requires true semantic specialization and preservation of common constraints. Similar fields alone do not justify inheritance.
- Public APIs remain candidate-only until a UI realization and a non-UI realization pass the same semantic checks.
- Architecture and semantic-baseline documents are locked at completion. The lock does not freeze final fields, inheritance, package/storage shape, or source migration.

## Testing Decisions

- Tests verify external semantic behavior and authority boundaries, not class layout or legacy one-to-one mappings.
- The first tracer bullet observes a page, creates Evidence and Segment, records multiple Slices, retains element Claims, creates an Effect, fixes a TargetBinding, records an Attempt timestamp, and evaluates post-click Evidence.
- Tests must prove that new observation does not overwrite historical Slice evidence; stale or ambiguous bindings fail closed; unknown delivery does not cause blind redispatch; and receipts do not prove World results.
- Deletion tests cover both expression preservation and evolution preservation: “update then convert” must agree with “convert then update”.
- Event tests first prove occurrence semantics; independent Event representation remains blocked until identity, count, timing, participants, causality, correction, or history cannot be preserved otherwise.
- The second realization must be non-UI and must use different domain payloads and locators while preserving the same semantic questions.
- Existing Evidence admission, Claim evolution, UI continuity/grounding, Effect dispatch, terminal outcome, and deterministic Async Perception tests are prior art.
- Test results support the candidate baseline only. They do not prove strict minimality, production readiness, or source migration completion.
- Documentation acceptance requires a citation and status for every important rule.

## Out of Scope

- Migrating or renaming the current Kernel implementation.
- Freezing final fields, class names, inheritance trees, namespaces, package names, serialization, storage, or transport.
- Building complete Browser, Mobile, Robot, or Simulation products.
- Selecting OCR, YOLO, DOM, planning, control, or perception algorithms.
- Making Slice globally versioned or forcing a universal freshness field.
- Freezing an independent Event type without counterexample and evolution evidence.
- Making Runtime Outcome, Verification, OpenDuties, Assurance, Trace, Projection, or Harness state a second Core fact authority.

## Further Notes

Source references are maintained in the companion document-alignment baseline and the locked Core to-spec. The governing materials are the vNext.1 Core design, the aligned extraction guide, the scenario library, the reviewer conclusion, the Target Product Architecture baseline, the frozen UIWorld L4 protocol, the Runtime/Simulation candidate design, the migration baseline, relevant ADRs, and the recorded deterministic validation evidence.

The repository has no configured external issue tracker. The Change State and this `spec.md` are therefore the durable publication and handoff surface. Any later change to the locked semantic baseline requires a new change record and an explicit counterexample or new evidence.
