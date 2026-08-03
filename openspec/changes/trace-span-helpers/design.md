## Context

Phase 1 (`trace-span-observability`, archived 2026-08-02) landed the `TraceSpan` span tree: `TraceSpan` record, `ITraceRecorder.StartSpanAsync`/`EndSpanAsync` (9-method interface, architecture-guard-locked), 18-member `SpanTypes` string catalog, and the `ITraceQuery` span-tree reads. An inventory of the current state (2026-08-03) found **34 manual call sites across 7 production files**:

| File | Sites | Shape |
|---|---|---|
| `TraversalEngine.cs` | engine.run (8 closes), engine.step (6), entry.generate (2), entry.ignored, entry.observed | multi-branch conditional closes, no finally; stateful passthroughs |
| `PageAnalyzer.cs` | ai.call (dual catch/success ends), ai.analyze (unpaired) | ~135-line method, retry-loop scoped |
| `SafetyGate.cs` | action.wait, action.click/scroll/back (dynamic spanType), entry.skipped | try/finally pairs + unpaired marker |
| `CompletionMonitor.cs` | analyze.error_loop/completion (dynamic ternary spanType) | span crosses `DecideActionAsync` await |
| `EnumerateCompletionAnalyzer.cs` | analyze.completion | straight-line pair, all start-attrs from locals |
| `ErrorLoopAnalyzer.cs` | analyze.error_loop | whole-dictionary attribute param, dynamic spanName |
| `InterceptionHandler.cs` | entry.visited | unpaired marker in static helper |

Common shape across sites: **span end-attributes are computed inside the span region from method-local variables** (`ExecuteAsync`'s `success`/`stopwatch`, `EnumerateCompletionAnalyzer`'s ten statistics, `CompletionMonitor`'s `DecideActionAsync`-returned bag), spanTypes are runtime-selected from catalog constants (ternary on `verdict.Reason`, `ActionToSpanType` switch), and two sites open spans only **after** a deny-gate check. All sites null-guard the recorder (traces are opt-in).

### Why not a `[TraceSpan]` source generator (verified 2026-08-03)

The previously proposed `trace-span-source-generator` change built a `[TraceSpan]` attribute + incremental generator with `"key:expr"` attribute expressions. Adversarial review against actual code established three constraints that make the attribute a poor fit for the current 34 sites:

1. **Attribute expressions can only reference method parameters and containing-class fields** — the generator wraps the original method, so locals computed inside the body are invisible to the emitted wrapper. 4 of the 5 sites classified "clean pairs" record attributes from locals (`EnumerateCompletionAnalyzer`'s `observed`/`visited`/`p50`/…, `ExecuteAsync`'s `success`/`stopwatch`, `CompletionMonitor`'s `verdict`/`finalAttributes`, `ErrorLoopAnalyzer`'s whole-dictionary parameter with per-call-site key sets). Only `WaitAsync`'s post-gate core (constant + parameter refs) is cleanly attribute-able — 1 of 34.
2. **Dynamic spanType is only a problem for the attribute** — `TraceSpanScope.BeginSpanAsync` takes spanType as a runtime argument, so `CompletionMonitor`'s ternary and `SafetyGate`'s `ActionToSpanType` flow in unchanged. The "fixed methods + dynamic dispatch" decomposition (previous D3) existed solely to satisfy the attribute's constant-only constraint; with scope-first recording it is unnecessary complexity.
3. **Deny-gate sites break whole-method annotation** — `WaitAsync`/`ExecuteAsync` open spans only after `decision.Allowed` (SafetyGate.cs L345-346, L383-388); a whole-method wrapper would record phantom spans on denied runs.

The generator is deferred (see "Deferred" section) and this change lands the two helpers that cover the actual shapes.

## Goals / Non-Goals

**Goals:**
- A `TraceSpanScope` async-disposable region scope (`await using var scope = await recorder.BeginSpanAsync(...)`, `scope.End(status, attrs)` or implicit dispose = `"ok"`) — the recording mechanism for ~20 of the 34 sites: local-computed attributes, runtime spanTypes, multi-branch terminal closes, deny-gate ordering.
- A `RecordEventAsync` point-in-time event helper for the 5 unpaired markers.
- Migrate all 34 manual sites to the helpers; delete hand-written `StartSpanAsync`/`EndSpanAsync` scaffolding from business code.
- Strict behavior preservation: same spanType/attributes/status/timing/parentage; denied runs still record no span; `input`/`long_press` still record no span.
- Additive Core change: the 9-method `ITraceRecorder` contract and its architecture guard stay untouched; new helpers are extensions.

**Non-Goals:**
- No `[TraceSpan]` attribute or source generator in this change (deferred to an independent change; see Deferred).
- No change to the `TraceHandlerGenerator` / `[TraceHandler]` mechanism.
- No change to the `SpanType` enum (constitution-locked, 11 values) or the 4 valid span statuses.
- No engine/hook/analyzer behavior changes beyond span recording sites.
- No method extraction that changes production method shapes solely to fit an annotation mechanism (e.g. `EnumerateCompletionAnalyzer` keeps its inline shape).

## Decisions

### D1 — `TraceSpanScope` + `BeginSpanAsync` extension, in Core

**Decision:** New `TraceSpanScope` (async disposable) + `ITraceRecorderExtensions.BeginSpanAsync(string spanType, string? spanName = null, string? parentSpanId = null, Dictionary<string, object>? attributes = null, CancellationToken ct = default)` declared as an extension on `ITraceRecorder?`. Returns a scope that (a) opens the span when the recorder is non-null and (b) is a side-effect-free no-op when it is null — `await using var scope = await recorder.BeginSpanAsync(...)` works for both, dropping the per-site null-guards. `DisposeAsync` ends the span with status `"ok"` if not already ended; `scope.End(status, attributes)` records an explicit close (status + merged final attributes) and is a no-op on double-close — same unknown/no-op `EndSpanAsync` semantics.

**Rationale:** End-attributes are almost always locals computed inside the span region; a scope API is the only mechanism that captures them without changing method shapes (no param-plumbing extraction, no `AttributeProvider` indirection). Placement in Core: `TraversalEngine` and `PageAnalyzer` live in Core and need the helper; Host placement would violate the Core→Host dependency rule.

### D2 — `RecordEventAsync` for the 5 unpaired markers

**Decision:** `ITraceRecorderExtensions.RecordEventAsync(string spanType, string? parentSpanId = null, Dictionary<string, object>? attributes = null, CancellationToken ct = default)` — opens a span and leaves it unclosed (`EndTime = null`, `DurationMs == 0` per TraceSpan.cs L35-36), the model's expression for point-in-time events. Replaces the 5 unpaired markers (`entry.observed`, `entry.ignored`, `entry.visited`, `entry.skipped`, `ai.analyze`). No-op when the recorder is null. `parentSpanId` is a runtime expression — method-call parents pass directly (e.g. `LatestEntryVisitedSpanId()`, SafetyGate.cs L471-477).

**Rationale:** 4 of 5 markers fire inside loops/conditionals (per-element or per-branch) — no whole-method annotation shape fits them; a one-line helper names the event semantics directly. Unclosed spans are the Phase-1 reality (tolerated by `IsCompleted => EndTime != null`); semantics unchanged.

### D3 — Deny-gate and runtime-spanType behavior preserved (no decomposition)

**Decision:** Migration preserves two existing behaviors exactly:
- **Deny-gate ordering:** `WaitAsync` and `ExecuteAsync` open their span only after `decision.Allowed` (SafetyGate.cs L345-346, L383-388). The scope is placed after the gate, so denied runs record no span.
- **Runtime spanType:** `ActionToSpanType(action)` (click/scroll/back + null for input/long_press/launch) and `CompletionMonitor`'s ternary stay as-is; the runtime value flows into `BeginSpanAsync`. No fixed-method decomposition (no `ExecuteClickAsync`/`ExecuteScrollAsync`/`ExecuteBackAsync` split).

**Rationale:** Scope accepts runtime spanTypes from the catalog constants, so decomposition buys nothing and would change method shapes and the input/long_press no-span behavior for zero benefit (verified: `ActionToSpanType` returns catalog constants only, SafetyGate.cs L479-485).

### D4 — Migration order: SafetyGate first, engine stateful last

**Decision:** Migration lands in tiers, each independently green with the span-tree tests as the behavior-equivalence oracle:
1. **M0** — helpers land + baseline frozen (Core, additive, with tests): record full-suite counts (AC5 anchor) and freeze `SpanTreeEquivalenceTests` S1–S5 snapshots from pre-migration behavior (AC1 anchor).
2. **M1** — `SafetyGate` (wait/execute scopes after the deny-gate, `entry.skipped` event).
3. **M2** — analyzer spans (`EnumerateCompletionAnalyzer`, `ErrorLoopAnalyzer` scopes; inline shapes kept).
4. **M3** — `CompletionMonitor` poll span scope + `PageAnalyzer` (`ai.call` scope with catch-path `"error"`/`ai.success=false` preserved, `ai.analyze` event).
5. **M4** — stateful `TraversalEngine` (engine.run/step/generate scope closes at each terminal branch, passthroughs retained as the seam; `entry.visited` → `RecordEventAsync` event; `entry.observed`/`entry.ignored` stay on the sync coordinator passthrough — `IDynamicChildManager.Generate` is sync-guard-frozen, `RecordEventAsync` is an async extension, so the emit path cannot await it; span-tree output is identical, verified by S1/S5).
6. **M5** — acceptance verification per the Acceptance Criteria matrix (AC1–AC6; no scaffolding outside helpers/passthroughs; baseline counts equal M0 records).

**Rationale:** Deferring the hardest stateful engine spans until the helpers are proven on Host-side sites keeps every step independently green (same staged-commit risk guidance as the Phase-1 archive).

## Risks / Trade-offs

- **[Risk] scope.End discipline** — with try/finally scaffolding removed, a terminal branch that forgets `scope.End` leaves an unclosed span. Mitigation: behavior is identical to today's conditional closes (which are also not in finally); the span-tree tests assert per-run trees; `DisposeAsync` auto-ends `"ok"` so scope-based sites without explicit status needs are safe by construction.
- **[Risk] Passthrough divergence** — `TraversalEngine` keeps its sync passthroughs (`_currentEngineStepSpanId` seam); engine.run/step/generate migrate to scope with `scope.End` at each existing terminal branch (same 8/6/2 close sites, same statuses). Mitigation: full mock-run span-tree assertions in M4 (root `engine.run`, per-step `engine.step`, parent chain intact).
- **[Risk] Event-span accumulation** — unclosed markers are the Phase-1 reality (5 sites) and `IsCompleted` already distinguishes them; semantics unchanged, documented as events not durations.
- **[Risk] Extension surface creep** — two extensions on a constitution-guarded interface could tempt further additive growth. Mitigation: architecture guard asserts exactly 9 interface methods + the two named extensions; any future helper needs a change.
- **[Risk] Behavior drift during migration** — each tier is a discrete commit; M1–M4 revert independently; M0 (helpers) is additive and can stay if migration halts.

## Deferred — `[TraceSpan]` source generator (independent change)

The `[TraceSpan]` attribute + `TraceSpanGenerator` (previously `trace-span-source-generator`) is deferred to a future change for attribute-first code (new spans written declaratively from day one, not retrofitted onto the 34 sites). Verified constraints for that change:

- **`"key:expr"` expressions resolve only against parameters and containing-class fields** — the wrapper cannot reference method locals; sites computing end-attributes from locals must use `TraceSpanScope` or an `AttributeProvider`.
- **A `TSG002` "spanType not in catalog" diagnostic** is the enforceable form of the catalog-membership guarantee (a runtime test cannot see generated code).
- **`TraceSpanGenerator` must be built fresh** — `TraceHandlerGenerator` remains the wrong foundation (sync-only, `ExecutionRecord`/`ITraceCoordinator`-targeted, hard-coded return-type schemas, zero tests).
- **Whole-method annotation cannot wrap deny-gate sites** (`WaitAsync`/`ExecuteAsync`) — those need post-gate extraction before annotation.
- **Distinct hint names** (`*_TraceSpan.g.cs` vs `*_Traced.g.cs`) so the two generators coexist; new `UniClaw.Core.SourceGen.Tests` project (`CSharpGeneratorDriver` snapshots) is required since no generator test project exists.

## Acceptance Criteria & Verification Plan

验收标准分两层：**每层差分快照等价**（行为等价性的机械证明）与 **最终验收矩阵**（脚手架清零 + 基线计数 + 零改动 oracle）。

### AC1 — 差分快照等价（行为等价性的机械证明）

M0 落地一个新的 `SpanTreeEquivalenceTests` 套件（Host.Tests，基于 `RunnerTestHarness` + `InMemoryTraceService`），先把**迁移前**的当前行为固化为规范化 span 树快照（spanType | spanName | status | parent | 排序后 attributes | 兄弟插入序，剔除时间戳/耗时），再通过 M1–M4 每层后该套件必须原样全绿。任一快照差异 = 该层验收失败，回滚该层。

五个场景（M0 从当前代码固化）：
| # | 场景 | 固化的不变量 |
|---|---|---|
| S1 | 成功枚举 mock run | `engine.run` 根 → N×`engine.step` → `entry.generate` → `entry.observed`/`visited`/`skipped` 混合子树 → `analyze.completion`（含全部属性键值） |
| S2 | safety deny 的 action | **无** `action.*` span（deny-gate 幽灵 span 回归防护）；`entry.skipped` 存在且 parent = 最近 `entry.visited` |
| S3 | 5 连 all-skipped error loop | `analyze.error_loop` 的 spanName=`"error loop: {reason}"`、`error.reason`/`error.consecutive_steps` 键值；正常 run 无该 span |
| S4 | AI 失败路径 | `ai.call` status=`error` + `ai.success=false`；`ai.analyze` 无 `EndTime`（事件语义） |
| S5 | 父链归属 | `entry.observed`/`ignored` 的 parent = `entry.generate`；`entry.visited` 的 parent = `engine.step`；`engine.step` 的 parent = `engine.run` |

### AC2 — 既有 oracle 套件零改动全绿

以下文件在迁移期间**不得修改**（`git diff --stat` 为空）且必须全绿，作为行为等价性的第二层证明（这些测试内嵌了迁移前的手写 span 断言）：

- Core.Tests：`TraceSpanTests`、`TraceSpanTreeTests`、`HandlerTraceWriterTests`、`InMemoryTraceRecorderTests`、`ArchitectureGuardTests`、`PageAnalyzerTests`、Traversal 7 文件（`TraversalEngineTests` 等）
- Host.Tests：`SafetyGateTests`、`ErrorLoopAnalyzerTests`、`EnumerateCompletionAnalyzerTests`、`CompletionMonitorTests`、`BaselineTests`

### AC3 — 脚手架清零

`grep -rn "StartSpanAsync\|EndSpanAsync" src/` 的命中**仅限**三类位置：
`ITraceRecorderExtensions.cs`（两个 helper 自身）、`TraversalEngine` passthrough 行（引擎记录接缝）、`ITraceRecorder`/`InMemoryTraceRecorder` 实现。`SafetyGate`/`PageAnalyzer`/三个分析器/`CompletionMonitor`/`InterceptionHandler` 及 TraversalEngine 非 passthrough 行**零命中**。

### AC4 — 目录成员资格

既有 catalog-membership 测试全绿（每个记录 spanType 都是 `SpanTypes` 18 成员）；`SpanType` enum 仍为 11 值（`EnumValueGuardTests`）；生成/新写代码不引入新 spanType。

### AC5 — 基线计数

完整套件通过数与 M0 记录基线**相等**：`dotnet test tests/UniClaw.Core.Tests` + `dotnet test tests/UniClaw.Host.Tests` 全绿，无新增失败、无既有测试被改（新增测试只增不减）；`UNICLAW_INTEGRATION_SCOPES` 门控的 emulator 集成测试保持原 skip 状态。

**基线（2026-08-03 实测，M0 起点）：** Core.Tests 1041 pass / 2 skip（VisionGolden + RealVision）/ 0 fail；Host.Tests 135 pass / 7 skip（emulator 门控）/ 0 fail。总计 1176 pass / 9 skip。迁移完成后通过数 ≥ 此数（新增 `TraceSpanScopeTests`/`RecordEventTests`/`SpanTreeEquivalenceTests` 只增不减）。

### AC6 — 无记录器零副作用

`TraceSpanScopeTests`/`RecordEventTests` 的 null-recorder 场景：无 `ITraceRecorder` 组合下 mock run 执行结果与裸代码一致、零 span、零异常。

### 每层验收方案（执行顺序）

1. `dotnet build`（Core + Host）→ 0 errors
2. 本层 oracle 过滤跑：M1 → `SafetyGateTests` + `SpanTreeEquivalenceTests`；M2 → 三个分析器测试 + S3；M3 → `CompletionMonitorTests` + `PageAnalyzerTests` + S4；M4 → Traversal 7 文件 + S1/S5
3. `SpanTreeEquivalenceTests` 恒绿（AC1 是每层的硬门槛）
4. 每层独立提交；失败回滚该层（M0 helpers 是增量基底，可保留）

## Open Questions

- **Q1 — PageAnalyzer `ai.call` shape.** Migrate inline with a scope (no method-shape change) or extract `CallModelAsync` then scope the extracted method (cleaner separation, shape change)? V1: inline scope — the migration goal is removing scaffolding, not reshaping the analyzer. Revisit if the retry loop grows.
