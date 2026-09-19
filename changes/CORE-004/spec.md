# CORE-004 — Core 与现有模型的责任对齐

Status: `READY FOR IMPLEMENTATION`
Triage: `ready-for-agent`
Publication: repository Change State（本仓库未配置外部 issue tracker）
Children: CORE-005, CORE-006（步骤子规格，不独立改变 Core 基线）

## Problem Statement

CORE-003 已建立并验证 Core 对象基线，但现有 Kernel、Agent、Perception、Assurance、
Runtime、Trace 和 UI realization 模型仍按原有职责组织。若直接按类名、字段相似度
或当前继承关系迁移，容易产生一一映射、第二套事实权威、视觉专用 Core、混合生命
周期和不可逆历史改写。

需要一个独立的责任对齐规格，规定如何判断现有模型与 Core 的关系，并允许在公共
职责上提取和下层减法。此 Change 不授权立即迁移源码，也不冻结最终继承树或字段。

## Solution

对每个现有模型先做责任分解，再选择一种或多种关系：

- 上提：公共且跨领域的语义进入 Core；
- 删除：Core 接管公共职责后，下层重复类、字段或校验删除；
- 合并：多个模型共同承担一个 Core 语义且没有独立事实权威；
- 拆分：混合 Core 语义、Runtime 状态、设备实现或 Harness 会话的模型按职责拆开；
- 直接替换：旧职责与 Core 语义相同且无需保留旧边界时替换；
- 继承：只有真正的语义特化且能保持共同约束时使用；
- 组合/引用：职责相邻但不属于同一语义类型时保持明确组合或引用；
- 保留在下层：专属于 Runtime、Perception、Assurance、Trace、设备、Harness 或
  场景实现的职责不强行进入 Core。

Core 的关系图只作为语义边界，不要求旧类保留、改名或与 Core 一一对应。

本 Change 采用父规格加步骤子规格的结构：

1. **World 对齐（CORE-005）**：Evidence → Segment/Slice → Claim；验证历史依据、
   Unknown、重叠观察和单一 World authority。
2. **Effect 对齐（CORE-006）**：Effect → Attempt → TargetBinding；先定义固定依据、
   请求快照和执行维度契约，再决定是否调整字段。
3. **Runtime 配套与旧模型减法**：只有前两步给出足够证据后才建立；处理 Projection、
   Runtime、Assurance、Trace、Harness 的保留/删除/组合关系。

CORE-005 和 CORE-006 可以并行收集事实，但不能并行改变 Core 语义；父规格负责
统一术语、单一事实权威和最终验收。

## User Stories

1. As an architecture owner, I want each old model classified by maintained responsibility, so that class names do not decide Core membership.
2. As a Core maintainer, I want public semantic duties promoted only when they are cross-domain, so that UI and device details stay below the boundary.
3. As a migration owner, I want duplicate lower-layer duties removed after promotion, so that two classes do not maintain the same fact.
4. As a World owner, I want Evidence, Segment, Slice, Claim, and Event responsibilities to have one canonical owner, so that projections and caches cannot become a second truth.
5. As an Effect owner, I want logical operation, Attempt, TargetBinding, delivery progress, and external result classified separately, so that a lifecycle enum does not mix them.
6. As a Runtime owner, I want state machines, recovery, persistence, scheduling, and coordination kept below or beside Core, so that Core remains a semantic protocol rather than a global transaction.
7. As a realization owner, I want UI occurrence, DOM, screenshot, ADB, spatial coordinates, robot pose, and SDK handles to remain domain payloads or realization contracts, so that Core stays domain-neutral.
8. As an inheritance reviewer, I want semantic specialization and shared constraints proven before inheritance, so that similar fields do not create an invalid subtype.
9. As a composition reviewer, I want mixed-responsibility models split or composed, so that they are not forced to inherit from whichever Core type shares the most fields.
10. As a history owner, I want old references, evidence, bindings, and revisions to remain reconstructable, so that refactoring cannot rewrite historical meaning.
11. As an authority owner, I want requirements and permissions to remain explicit Clause semantics, so that observation, claims, or subject references cannot grant authorization.
12. As a validation owner, I want each alignment choice tested at the highest existing seam, so that tests prove semantic preservation rather than class shape.
13. As a change owner, I want each unresolved guarantee to block only the uses that depend on it, so that a local migration gap does not become an unbounded project block.
14. As a product owner, I want this alignment pass to preserve the Core minimum and allow lower-layer subtraction, so that extraction reduces rather than duplicates the model surface.

## Implementation Decisions

- The primary artifact is a responsibility matrix, not a migration patch. Every candidate
  row records: current responsibility, Core semantic candidate, non-Core responsibility,
  authority owner, history/evidence obligations, possible relation, and unresolved gate.
- The matrix must distinguish semantic ownership, internal composition, reference relation,
  class inheritance, transaction aggregation, and physical storage. One must not be
  inferred from another.
- World candidates are checked against Evidence → Claim/Event → Segment/Slice history;
  UI continuity, locator, perception, and reconciliation mechanics remain realization
  responsibilities unless a cross-domain semantic duty is proven.
- Effect candidates are checked against Effect → Attempt → TargetBinding and against the
  separate dimensions of delivery progress, external execution/result, and coordination.
  A receipt, Runtime Outcome, Assurance judgment, or terminal state cannot silently become
  a Core fact.
- Specification candidates are checked for explicit source, authority, scope, validity,
  and revision relations. Evidence or subject references do not create permissions.
- Inheritance is allowed only when the subtype is a true semantic specialization, preserves
  field meanings, history, evidence, uncertainty, and permission boundaries, and does not
  weaken any common constraint. Field similarity alone is insufficient.
- Mixed models may be split or composed. No empty subclass, wrapper, or Adapter is created
  solely to preserve a one-to-one shape.
- Core must not depend on concrete scenario classes, device SDKs, recognition libraries,
  Harness session types, or current UI realization classes. Concrete coordinates, paths,
  poses, and resource locators may remain in realization representations with explicit
  contracts.
- A missing BasisRef, request snapshot, authorization reference, executor reference,
  dispatch log, or other required guarantee blocks only the dependent use. It cannot be
  replaced by an opaque JSON field or a pending TODO.
- The final decision for each row is deferred until the row has a scenario-backed semantic
  test. This Change does not freeze package layout, storage, public API, or final field names.

## Testing Decisions

- Tests assert preserved semantic behavior and authority boundaries at existing public seams;
  they do not assert that an old class remains, that a new class inherits, or that names map.
- Each alignment candidate needs at least one positive path and one negative or history path:
  old evidence remains inspectable, old bindings do not follow latest, unknown does not become
  success, and permissions do not arise from observations.
- Use the existing Core-only tests for protocol behavior, Kernel projection seam tests for
  the current UI realization, and existing World/Effect/Outcome/Simulation tests as evidence
  of lower-layer responsibilities. Do not count projection tests as proof of final migration.
- Inheritance decisions require a substitutability test that preserves shared constraints;
  composition or split decisions require tests proving no second fact authority appears.
- Deletion decisions require an evolution-preservation check: update then align and align then
  update must retain the same required distinctions.
- Any test relying on a legacy class signature, sealed modifier, or field list is evidence
  about the current implementation only, not an architectural decision.

## Out of Scope

- Implementing a complete source migration or deleting old models in this Change.
- Choosing final Core field names, package names, inheritance trees, serialization, storage,
  or transport protocols.
- Building a second non-UI realization.
- Replacing Runtime state machines, Assurance, Trace, Perception, device drivers, or Harness
  sessions with Core types.
- Adding generic freshness, trust, confidence, correlation, or version fields without a
  concrete semantic buyer and guarantee.
- Treating a passing full-suite run as proof of strict minimality or final architecture.

## Further Notes

The governing baseline is CORE-001/CORE-003 plus the vNext.1 design, document-alignment
record, scenario library, and recorded grill-with-doc evidence. The first deliverable of
this Change is the responsibility matrix and its unresolved gates. Only rows with a clear
semantic decision and sufficient guarantees may proceed to a later implementation Change.

The repository has no configured external issue tracker; this `spec.md` and its Change State
are the durable publication and handoff surface.

CORE-004 只有在所有已建立子规格完成验证、每个候选模型都有 Owner/Authority/Relation
记录、且没有未登记的第二事实权威时才可关闭。子规格完成不等于旧源码迁移完成。
