# Guard 5 — Trap Boundary

> 状态: Active | Phase: 2A (原子修订) | 日期: 2026-08-08
> 关联: HG-1 Guard 5 Revision Protocol (phase2-human-gate-decision.md)
> 对应 [Fact]: `RuntimeSource_TrapTypesAllowedOnlyInModelOrRecovery` / `RuntimeSource_RecoveryRequestType_BannedEverywhere`

## Rule

```
Trap / TrapKind / TrapScope 类型声明仅允许在:
  - src/UniClaw.Runtime/Model/   — 不可变值类型定义
  - src/UniClaw.Runtime/Recovery/ — 恢复组件

其他目录 (Agent / Container / Traversal / Startup / World / Environment) 仍禁止

RecoveryRequest 类型声明:
  全目录禁止 (含 Model/ 与 Recovery/)
```

正则（声明级，长名优先，注释感知）:
- Trap 家族: `\b(?:record|class|struct|interface|enum|delegate)\s+(?:TrapKind|TrapScope|Trap)\b`
- RecoveryRequest: `\b(?:record|class|struct|interface|enum|delegate)\s+RecoveryRequest\b`

只匹配"声明关键字 + 类型名"模式，不匹配注释 / 正文讨论文字。长名优先（TrapKind / TrapScope 先于 Trap）避免正则交替短名吞前缀。

## Evidence

- **HG-1** (Guard 5 Revision Protocol, Approved 2026-08-08): Trap 类型仅限 Model/ + Recovery/；RecoveryRequest 保持全目录禁止
- **裁决 4**: Trap 一等模型由 Phase 2 引入（Phase 1 的 `TraversalStepResult.Failed(Reason)` 为最小 escalate 表面）
- **I-8** (escalate 不偷权): 低层组件以结构化结果上报，不得自行取得高层权威
- **I-13** (God Context 防范): 禁止 Observation / WorldBelief / RuntimeState / Memory 重新聚合
- **tasks.md A6**: Guard 5 修订与 A1（Trap 类型定义）原子完成，防 guard 空窗

## Scenario

- **SC-P2-001** (Agent Recovery — Launcher Drift): Trap(Scope=Agent) 首次端到端断言消费。Evidence 1: TrapKind=UnexpectedPage, TrapScope=Agent, Expected/Observed 为观测序号引用

## Reason

Trap 是数据定义（Model）+ 恢复语义（Recovery）的产物。出现在执行层（Agent / Container / Traversal）意味着 Trap 决策 / 发射逻辑泄漏进执行组件，破坏裁决 4 的组件边界——Trap 是 evidence 载体，不是执行组件的内部业务模型。

`RecoveryRequest` 保持全目录禁止：HG-5 明确 Phase 2 不引入统一恢复机制（RecoveryRequest / Planner / Runtime）；Phase 2 只买最小 Recovery scope。

## Violation Example

### Trap 类型声明位置违规
```csharp
// ❌ Agent/Agent.cs — Trap 类型声明在执行组件
record Trap { ... }
```
→ Guard 失败信息: "Trap 类型只允许落在 Model/ 或 Recovery/"

### RecoveryRequest 类型声明违规
```csharp
// ❌ Model/RecoveryRequest.cs — RecoveryRequest 全目录禁止 (HG-5)
sealed record RecoveryRequest { ... }
```
→ Guard 失败信息: "RecoveryRequest 全库禁止"

### 合法位置
```csharp
// ✅ Model/Trap.cs — 不可变值类型定义
// ✅ Recovery/Recovery.cs — 恢复组件（无 Container/Traversal 引用）
```

## State

Active — 修订自 Phase 1 "全目录禁止 Trap"，Phase 2A 原子完成 (HG-1)。
