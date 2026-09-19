# ADR-0024：UI World realization 是 Kernel World 内模块，落出向依赖边界执法；程序集升格推迟

## Context

CORE-015 把「Core 提取为顶层抽象契约、UI World 作为第一个按契约实现的
realization」推进到物理拆分（Step 4）。对齐表确认 `WorldModel` 混装两种
东西：领域无关的世界机制（Evidence→Belief reconciliation、claim、冲突
保留、revision 推进）与 UI 专属（Container、Occurrence、LogicalItem
连续性、UI grounding）。realization 契约 v0.1 与双向验证（7 场景 × 两
方向）已完成；Human 于 2026-09-19 在三选项中裁决选项 3 并采纳「模块级
两步走」的工程化建议。

## Decision

1. **UI World realization = Kernel World 内部模块**：UI 专属类型
   （UiEntityModel / Continuity / ContainerAssociation / GroundingSeam /
   OccurrenceDescriptorMatcher）落 `src/UniClaw.Kernel/World/UiRealization/`
   （命名空间 `UniClaw.Kernel.World.UiRealization`）；通用机制、Slice、
   consumer view、locator 协议材料留在 World owner。
2. **边界以测试执法，不靠约定**：`UiRealizationBoundaryTests` 锁定模块
   出向依赖白名单——只允许 System / UniClaw.Core / World owner /
   Evidence 输入缝（契约 §2 声明输入）/ 自身；对 Control / Assurance /
   Effects / Run / Trace / Perception / Diagnostics / Agent 的任何依赖
   即红。这是 realization 契约「不得拥有」清单的类型级化身。
3. **入向组合引用不受该边界约束**：单 realization 现状下 revision 即
   UI realization 产物（契约 §3），owner 与 Kernel 编排方消费模块缝
   （含 P23 声明的 demand 生产者，如 `RunObligation.EntityScope`）是
   设计事实，不是越界。
4. **程序集升格推迟**：UiRealization 不建独立 csproj，触发条件 = 第二
   个 realization（文件/API/机器人）开工（NO_REAL_BUYER 判据）；届时
   依赖边界测试保证升格只是目录变 csproj，不需要重新理边界。
5. **纯搬移与形状重构分刀**：本次零形状改动；「UI 类型向 Core 形状
   对齐」（调整自由度，Human 已批）是独立后续 Change，不与搬移混合。

## Considered Options

- **选项 1：Kernel Owner 内部组合（不动结构）**：拒绝。缝只存在于文档
  与约定，与本仓「结构保证优先」的一贯打法（程序集纯度测试、纪律扫描、
  InternalsVisibleTo 执法）相悖；Step 4 将空转。
- **选项 2：立即独立程序集**：拒绝。WorldModel 的通用一半会被拖错位置
  （Human 明确警告不得把整个 WorldModel 原样搬成 UI Core），或被迫再建
  第三个通用世界模块；且无第二个 realization 消费该程序集边界——
  为无买家结构预付成本。
- **模块级 + 执法测试 + 升格条件（接受）**：顺着已语义分好的缝切，
  边界即刻可执法，升格路径与成本后置到有买家时。

## Consequences

- realization 契约的负向清单成为每次测试运行都检查的结构事实；
  「UI 管领域实现细节、不越界」可被红绿证明。
- 升格（第二个 realization 出现时）是机械动作：目录 → csproj + 边界
  测试同步迁移；依赖方向已锁死，不会重新谈判。
- 遗留可选项（有记录、无欠账）：UI 类型向 Core 形状对齐；Claim 谓词
  与 Attempt 三轴投影增强（数据源 = CORE-013 执行源）；Claim/Evidence
  之外的 UI identity 命题表达。
- 本 ADR 不主张：UI World 已可脱离 Kernel 运行；通用世界机制与 UI 的
  revision 级分离已完成（那是第二个 realization 强制的问题）。
