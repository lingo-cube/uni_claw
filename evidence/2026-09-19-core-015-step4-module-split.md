# Evidence — CORE-015 Step 4 模块拆分（选项 3，模块级）

> Change: `changes/CORE-015/state.md`
> Authorization: Human 2026-09-19 裁决「3」（选项 3：拆 UI 专属类型、
> 编排留 Kernel；采纳工程化建议的模块级两步走——程序集升格推迟）
> 契约: `docs/design/uiworld-realization-contract-v0.1.md`

## 1. 拆分内容（纯搬移，零形状改动）

| 动作 | 内容 |
|---|---|
| 搬移（5 文件 → `src/UniClaw.Kernel/World/UiRealization/`，命名空间 `UniClaw.Kernel.World.UiRealization`） | `UiEntityModel.cs`（OccurrenceBelief / LogicalItemBelief / ProposedOccurrence / LogicalItemLifecycle / IUiObservationStrategy）· `Continuity.cs`（demand / handle / proposal / IContinuityStrategy / 判别类型）· `ContainerAssociation.cs`（ContainerIdentity / ContainerGraph / IAssociationStrategy / 关联类型）· `GroundingSeam.cs`（GroundingView / TargetDescriptor / 接地缝）· `OccurrenceDescriptorMatcher.cs`（匹配器 + ContainerMatchMode） |
| 留在 World owner | `WorldModel` / `WorldBeliefRevision` / `WorldRevisionIndex` / `PersistentRevisionCollections` / `Slice`（qspec 定为 World 通用结构）· `ConsumerViews` / `TransitionContext`（owner 协议面）· `SpatialLocator` / `NativeLocator`（跨 EB/driver 的协议材料）· `RelevanceJudgment` |
| 附带（编译器驱动的机械改动） | ~40 个 src/tests 文件的 using 补充；1 处全限定名更新（BundleIntegrityTests） |
| 新增执法 | `tests/UniClaw.Kernel.Tests/UiRealizationBoundaryTests.cs`：模块源文件 using 面只允许 System / UniClaw.Core / World owner / Evidence 输入缝（契约 §2 声明输入）/ 自身——Control/Assurance/Effects/Run/Trace/Perception/Diagnostics/Agent 任何依赖即红 |

## 2. 方向说明（诚实边界）

- 约束的是模块**出向**依赖（UI realization 不得长触手）；owner → 模块的
  入向组合引用不在本测试约束内——单 realization 现状下 revision 即 UI
  realization 产物（契约 §3），`RunObligation.EntityScope → TargetDescriptor`
  是 P23 声明的 demand 生产者缝（CONTEXT：ContinuityDemand 生产者封闭）。
- 程序集升格（UiRealization → 独立 csproj）**推迟**：NO_REAL_BUYER 判据，
  触发条件 = 第二个 realization（文件/API/机器人）开工；届时依赖方向
  测试保证升格只是目录变 csproj，不是重新理边界。
- UI 类型向 Core 形状对齐（调整自由度）未在本刀执行——纯搬移与形状
  重构不混刀；作为后续可选 Change。

## 3. 过程留痕

机械搬移中一次 perl 误删了 6 个测试文件的文件头（using 块 + 文档注释）
——立即 `git checkout` 还原到含 Step 3 的最新提交后以正确方式重做；
最终 diff 经全量回归验证无行为变化。

## 4. 验证声明

```yaml
level: DETERMINISTIC
method: dotnet test UniClaw.Kernel.slnx 全量 + 边界测试单独运行
expected: 570/570 全绿（569 基线 + 1 新边界测试）；零行为变化；git diff --check 通过
actual: >
  Core 14 + Agent 17 + Simulation 132 + Kernel 407 = 570/570 全绿；
  UiRealizationBoundaryTests 通过；git diff --check 通过
evidence: 本文件 + 全量测试输出
```
