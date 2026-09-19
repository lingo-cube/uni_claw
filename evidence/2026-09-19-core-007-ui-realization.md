# CORE-007 UI realization 验证记录

日期：2026-09-19  
范围：vNext.1 最小候选基线审阅锁定后的 UI 首版投影。  
不包含：旧 UI/Kernel 模型迁移、机器人或其他领域 realization、最终字段冻结。

## 结论

- 审阅锁定的核心记录为 `Clause`、`Segment`、`Evidence`、`Claim`、`Effect` 五种。
- `Slice` 是 World 的条件内部结构；`Attempt` 和 `TargetBinding` 是 Effect 的执行内部结构。
- Event 发生语义继续由 `Evidence + Claim + 引用/时间` 表达；本轮没有新增独立 Event 类型。
- UI 使用已有 `UniClaw.Kernel.Core.CoreSemanticProjection` 作为唯一只读投影缝，没有新增第二事实权威。

## 本轮实现

`CoreSemanticProjection.ProjectClauses(ExecutionContractView, DateTimeOffset)` 将已接受 UI
contract 中明确给出的 `Objective` 投影为 `ClauseKind.Requirement`，将
`ProofCriteria` 投影为 `ClauseKind.Criterion`。

`AllowedEffects` 和 `ForbiddenEffects` 仍留在 Runtime contract。当前 contract view 没有
足够独立的授权来源、范围和有效条件时，不把短字符串自动提升为 `Permission`，避免由
观察或执行配置制造权限。

当前 UI 入口的 `TargetBinding` 使用 Slice 作为固定 basis；Core 规则不强制 Slice，非 UI
realization 仍可在具备契约时使用 Evidence、Claim 或 ResourceVersion。Attempt 的请求
快照、执行端、授权依据和三条状态轴同样属于用途条件字段，缺少时只阻塞依赖它们的生产
用途。

同一 projection seam 继续覆盖 UI 的 Evidence、Segment、Slice、Claim、Effect、Attempt、
TargetBinding；滚动—点击场景的历史 Slice、过期 binding、Unknown 投递结果和 locator
投影测试保持通过。

## 验证命令与结果

```text
dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --no-restore
通过 13，失败 0，跳过 0

dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore
通过 389，失败 0，跳过 0

dotnet test UniClaw.Kernel.slnx --no-restore
Core 13、Kernel 389、Agent 17、Simulation 112，合计 531；失败 0
```

存在既有 `NU1900` 漏洞缓存权限警告及既有 XML/nullable/xUnit analyzer 警告；没有新增
失败。测试通过只证明当前投影缝和已有场景契约，不证明严格最小性或跨领域迁移完成。

## 后续关口

先用本版指南检查实际模型，再用实际模型和场景反驳指南。只有出现表达保持或演化保持
反例，才新建 Change 决定修改下层、Core 或指南；不得因 vNext.1 的可选字段清单直接
扩张 Core。
