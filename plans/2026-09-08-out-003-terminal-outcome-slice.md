# Plan — OUT-003 Terminal / Outcome Vertical Slice

> PlanType: out-003-terminal-outcome-slice / Status: ADOPTED / References:
> changes/OUT-003/state.md · docs/architecture/product-architecture-baseline-l0-l3.md
> （Target v0.1）· changes/E2B-001 · changes/C2E-002

## 垂直切片：行为 → 模型 → 运行时 → 测试 → 证据

终局链（一种行为，贯穿五个 Owner 层）：

```text
Accepted Evidence + Current WorldBelief + Run-level Proof Obligation State
        ↓  Assurance.JudgeOutcome()                    （sole Outcome Proof Authority）
OutcomeProof (Completion | Failure | SafeStop | Escalation)
        ↓  RunModel.TransitionToTerminal(proof, expectedPrior)   （exact-prior CAS）
terminal OutcomeState（记录 proof 快照，不重判）
        ↓  UniKernel.EvaluateTerminal() → EffectBoundary.CloseDelivery() → emit once
immutable RuntimeOutcome（RunId + classification + obligations + proof ref + evidence refs）
```

## Before / After

### Before（C2E-002 收盘面）

- `ProofObligationState(IReadOnlyList<string>)` —— 占位，只记录存在（C2E D3）
- 无 Outcome State / Outcome Proof / Runtime Outcome / terminal 语义
- `RuntimeAssurance` 只有 action-local Judge；`EffectBoundary` 无 delivery 关闭
- `RunModel.Transition` 无 terminal 守卫：Cycle/Action 可无限推进
- UniKernel 无终局编排（Act pipeline 已存在）

### After

| 文件 | 变化 | Owner |
|---|---|---|
| `Run/RunObligation.cs`（新） | `RunObligationKind`、`RunObligation`、`ObligationStatus`、`TerminalClassification` | Run Model 词汇 |
| `Run/OutcomeState.cs`（新） | `OutcomeState`（记录快照）、`OutcomeTransition`（CAS 结果） | Run Model |
| `Run/RunState.cs` | `ProofObligationState` 内容改为 `IReadOnlyList<RunObligation>`；`RunState` 增 `Outcome`（terminal 后非 null） | Run Model |
| `Run/ExecutionContract.cs` | 增可选第 7 参 `Obligations`（默认 null；C2E 行为不变） | UniAgent 侧产物（本片测试脚本构造） |
| `Run/RunModel.cs` | `RunId`、`IsTerminal`、obligations 派生、`TransitionToTerminal`（exact-prior CAS）、terminal 后 Cycle/Action fail-closed | Run Model |
| `Assurance/OutcomeProof.cs`（新） | `OutcomeProof`（Assurance 产出） | Assurance |
| `Assurance/RuntimeAssurance.cs` | 增 `JudgeOutcome(view, obligations, current, canonical)` —— 四分类确定性判定 | Assurance |
| `Effects/EffectBoundary.cs` | 增 `IsDeliveryClosed` / `CloseDelivery()`；Dispatch 首查 latch（fail-closed） | Effect Boundary |
| `UniKernel.cs` | `Act`/`SelectIntent` terminal fail-closed；`AdmitContract` 透传 obligations；`EvaluateTerminal()` 编排 + `TerminalEvaluation`；emit exactly once | Uni Kernel |
| `Outcome/RuntimeOutcome.cs`（新） | `RuntimeOutcome`（immutable envelope） | Uni Kernel |
| `tests/.../TerminalOutcomeTests.cs`（新） | 验收 1-10 + 反例 A-J 的确定性用例 | —— |

## 关键语义（固化，防漂移）

1. **obligation 满足 = evidence-backed claim**：subject=value 现于 current
   revision 的 WorldState 或 Conflicts，该 claim 的 EvidenceId ∈ basis。
   MaterialEffect 额外要求 backing record producer 前缀 `effect.boundary`
   （D7 自产观察）——receipt（producer `effect.boundary`、subject `attempt.*`、
   世界无关）永不满足 effect obligation。
2. **四分类优先级（确定性）**：mandatory Failure → SafeStop → Escalation →
   全 mandatory satisfied → Completion；否则无 proof（non-terminal 不猜测）。
3. **terminal 单次性**：transition exact-prior（引用等同）CAS + kernel emission
   latch + EffectBoundary delivery latch 三层不变量 42/39。
4. **Owner 零穿透**：RunModel 不读 Evidence/判断；Kernel 不重判；Receipt/Control/
   Provider 不写 OutcomeState；terminal 后 evidence 仍可 admission（E2B 路径），
   但永不回写 Outcome。

## TDD 次序（RED → GREEN → REVIEW → VERIFY）

1. RED：先写 `TerminalOutcomeTests.cs`（验收 10 + 反例 A-J），行为方法桩
   （NotImplementedException / 空实现），运行可见关键终局场景失败
2. GREEN：按上述 After 表实现最小代码（不顺手重构）
3. REVIEW：fresh SubAgent 六轴（第二 Completion Authority / Kernel 重判 /
   terminal 后 effect / receipt 冒充 completion / RunModel 判证据 / parallel
   outcome state）
4. VERIFY：全量 `dotnet test`（E2B 8 + C2E 10 + OUT ~16）；no-drift 检查；
   evidence 落盘；CONTEXT.md 术语收编

## 验收 ↔ 反例 ↔ 用例映射（草案）

| 验收 | 覆盖 | 用例（TerminalOutcomeTests） |
|---|---|---|
| 1 Assurance 独占 | 反例 I/J | Outcome16/14/15 |
| 2 Run 只记录 | 结构检查 | Outcome16 |
| 3 completion 需 mandatory 全满足 | C | Outcome1/4 |
| 4 receipt ≠ completion | A | Outcome1 前半/2 |
| 5 verified effect ≠ completion | B | Outcome3 |
| 6 failure/safe-stop 独立 proof | D | Outcome5/6 |
| 7 exact-prior single-winner | F | Outcome8/9 |
| 8 immutable exactly-once | —— | Outcome10 |
| 9 terminal 后 effect closed | 八 | Outcome11 |
| 10 late 不能重写 terminal truth | G/H/I | Outcome12/13/14 |
| 附加:E 不猜测 / J 控制≠完成 | E/J | Outcome7/15 |

## 停止条件（本 Plan 完成后不自动启动）

Goal Evaluation slice / Memory slice / Legacy cutover / 多 Run / continuation /
F2 架构重构——一律不做，只输出推荐下一 change。