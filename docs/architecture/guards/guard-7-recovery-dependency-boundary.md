# Guard 7 — Recovery Dependency Boundary

> 状态: Active | 预置: Phase 2A closeout | 首次非平凡执行: Phase 2B B3
> 关联: HG-1 Guard 5 Revision Protocol (phase2-human-gate-decision.md) + design.md §7
> 对应 [Fact]: `RuntimeSource_Recovery_HasNoContainerOrTraversalNamespaceReferences`

## Rule

```
Recovery/ 目录下所有 .cs 文件不得包含:
  - "UniClaw.Runtime.Container"
  - "UniClaw.Runtime.Traversal"

恰好 2 项禁令。无更多限制 (HG-1 冻结边界)。
纯 Contains 扫描 — alias import 同样命中:
  using RuntimeContainer = UniClaw.Runtime.Container.Container;  // ← 命中
```

Guard 7 是**预置围栏**：Phase 2A closeout 时立规（Recovery/ 仅 .gitkeep，零 .cs 平凡通过），Phase 2B B3 写入 `Recovery.cs` 后首次非平凡执行。

## Evidence

- **HG-1** (Guard 5 Revision Protocol, Approved 2026-08-08): "新 Guard: Recovery 组件不得引用 Container/Traversal 内部实现"
- **I-1** (依赖方向): Agent → Container → Traversal → Environment spine。Recovery 是支持能力（§4），经 Agent → Recovery → Environment 路径，不属执行链
- **design.md §7**: Guard 7 新增；Recovery 组件禁止引用 Container/Traversal 内部实现
- **§41** (Architecture Tests): 能够自动验证的规则必须加入 Architecture Tests

## Scenario

- **SC-P2-001** (Agent Recovery — Launcher Drift): 恢复机制消费 RestoreRecipe + ExecuteNextAsync(LaunchApp) → Observe → Verify。Evidence 6: action history 无低层恢复动作（恢复只经 Recovery → IEnvironment，不经 Container/Traversal）
- **SC-P2-003** (Recovery Verification Failure): 验证失败 → Agent 终止 Run。Recovery 组件返回 `Failed(Reason)`，不触碰 Container/Traversal

## Reason

Recovery 是支持能力（Charter §4），不属于 Agent → Container → Traversal → Environment 执行链。依赖执行组件（Container.Bind / Traversal.ExecuteStep）意味着：
1. 恢复机制耦合进 traversal 执行面（design.md §7"恢复机制独立于执行"）
2. I-1 依赖方向被反向（执行层变成恢复路径的上游依赖）
3. HG-4 的"组件持机制 / Agent 持决策"分离被模糊——组件获得操作 Container/Traversal 的能力即获得隐性执行 authority（I-8 违反）

## Violation Example

```csharp
// ❌ Recovery/Recovery.cs
using UniClaw.Runtime.Container;   // ← Guard 7 失败
using UniClaw.Runtime.Traversal;   // ← Guard 7 失败

// ❌ 同样命中
using RuntimeContainer = UniClaw.Runtime.Container.Container;
```

### Guard 失败信息
- what: `{RecoveryFile} 引用了禁止的 namespace「UniClaw.Runtime.Container」`
- why: Recovery 组件依赖执行组件破坏 I-1 依赖方向（HG-1 冻结边界）
- read: Architecture Contract I-1 + HG-1 + phase2 design §7

### 合法用法
```csharp
// ✅ Recovery 组件仅依赖 Environment + Model + BCL
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
```

## Timeline

| 时间 | 状态 |
|------|------|
| Phase 2A closeout | Guard 7 预置（Recovery/ 仅 .gitkeep，零 .cs 平凡通过）|
| Phase 2B B3 | Recovery/Recovery.cs 写入，首次非平凡执行通过 |
| Phase 2C C4 | SC-P2-001 正式场景测试通过（Guard 7 持续通过）|

围栏先立规后写码——与 Phase 1 Guard 2 在旧代码引用前落地的模式一致。

## State

Active.
