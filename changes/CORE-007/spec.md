# CORE-007 — vNext 最小候选基线审阅与 UI realization 实现

## Problem Statement

vNext.1 定义了完整的跨领域语义和字段等级，但它不是要求所有字段都进入第一版
Core 实现的字段清单。当前需要把“语义必须保留”和“字段/类型必须存在”分开，
基于 vNext.1、场景材料、审阅汇总和现有 UI realization，锁定一版最小候选基线，
再实现一版 UI 侧 realization；不能因 UI 的字段相似就提前决定机器人或其他领域的
抽取方式。

## Solution

先审阅并锁定：

- 五种 Core 核心记录：`Clause`、`Segment`、`Evidence`、`Claim`、`Effect`；
- `Slice` 是 Segment 下按用途需要的局部观察表示；
- `Attempt`、`TargetBinding` 是 Effect 内部执行职责；
- Event 的发生语义必须保留，但暂不冻结独立 Event Core 记录；
- 字段按建档必填、用途条件必填、追加记录和展示选填分类，不照搬 vNext.1 全部字段。

再以现有 Kernel UI realization 作为第一版实现缝：只读投影到上述 Core 语义，不建立
第二事实权威，不把 UI 专用 locator、occurrence、driver、receipt 生命周期提升为
跨领域 Core。

## User Stories

1. As a Core reviewer, I want the minimal candidate set to be explicit, so that field
   references in vNext.1 are not mistaken for mandatory implementation classes.
2. As a World owner, I want Segment continuity and Slice local observation preserved,
   so that scrolling and partial coverage do not rewrite history.
3. As an Evidence owner, I want provenance and observation context preserved, so that
   missing context blocks only the dependent use.
4. As a Claim consumer, I want attribute, relation, occurrence and causal judgments
   expressible without an independent Event authority, so that facts and judgments remain
   separate.
5. As an Effect owner, I want logical operations separated from Attempts and bindings,
   so that retries, unknown outcomes and historical targets remain distinguishable.
6. As a UI realization owner, I want the existing scroll-and-click path projected through
   one seam, so that UI-specific details remain below Core.
7. As a future non-UI realization owner, I want no UI field to become a mandatory Core field,
   so that resource, robot and simulation cases can be evaluated later.
8. As a reviewer, I want every omitted vNext.1 field to have a reason and blocking scope,
   so that omission is not hidden as an implementation shortcut.

## Implementation Decisions

- vNext.1 is the reference design for meaning, constraints and field levels; this Change
  selects a smaller implementation baseline and does not rewrite vNext.1.
- The review baseline is five Core records. Event semantics remain mandatory; independent
  Event representation requires a concrete expression or evolution counterexample.
- `Slice`, `Attempt` and `TargetBinding` remain explicit semantic structures but are not
  counted as additional top-level Core records.
- The existing `UniClaw.Kernel.Core` projection is the sole UI realization seam. Do not add
  a parallel UI Adapter or copy Kernel World/Effect authority into Core.
- A vNext.1 field enters the UI implementation only when it is required to answer a current
  UI question, required by a declared use gate, or needed to preserve a fixed reference.
- A missing conditional field remains missing/Unknown and blocks only the use that depends
  on it; the implementation must not fabricate currentness, authorization, success or
  external identity.
- UI locator and occurrence material may remain opaque realization values at the seam.
  Their extraction for robot, file/API or simulation remains undecided.
- No final inheritance tree, storage schema, serialization protocol or legacy migration is
  decided by this Change.

## Testing Decisions

- Review tests compare required semantic questions, not the number of classes or fields.
- The UI tracer must preserve Segment/Slice history, Evidence/Claim separation, Effect vs
  Attempt, fixed binding basis, Unknown delivery and post-action Evidence evaluation.
- Field omission tests must distinguish absent, Unknown, not applicable and not yet admitted.
- Event reduction tests must preserve occurrence identity, count, time, participants, basis
  and correction history where the UI scenario needs them.
- Existing Core and Kernel projection seams remain the primary test surface; no new seam is
  introduced unless the current public seam cannot express a required distinction.

## Out of Scope

- Strict mathematical proof of minimality.
- A robot, file/API or simulation realization.
- Migrating or deleting existing UI/Kernel model classes.
- Making every vNext.1 field mandatory in Core.
- Introducing an independent Event, Expectation, Verification, Outcome or OpenDuties Core
  authority.
- Freezing final public API, inheritance, persistence or transport contracts.

## Further Notes

The review lock is recorded in `docs/design/core-extraction-qspec-v0.2.md` and
`evidence/2026-09-19-vnext-field-minimal-review.md`. UI implementation follows this review
inside the same Change; future realization extraction requires its own scenario-backed review.
