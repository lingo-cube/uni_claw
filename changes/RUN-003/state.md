# RUN-003 — Kernel 驱动面最小公开化（Product Host 组合缝）

lifecycle_state: planning · disposition: none · depth: standard · base: 51dbb6c4

## Intent

把 Product Host 组合所需的 Kernel 驱动面从 internal 提升为 public，
零行为变化，白名单执法。HOST-001 D8 前置：不改这层，Host 无法编译
`KernelRunDriver(kernel, planPolicy, RunDriverInputs{...})`。

## 已核实事实

- `KernelRunDriver`（internal sealed，ctor internal）、`RunDriverInputs`
  （internal sealed）、`RunDriverInput`（internal abstract，嵌套
  Observation/Cancel/Unexpected）、`AgentDecisionContext` /
  `AgentActionProposal` / `AgentDecision`（internal，Runtime/AgentDecision.cs）、
  `RunDriveResult` / `RunDriveStatus`（internal，KernelRunDriver.cs）；
- IVT 现仅测试程序集；RunDriverInputs 自述「Simulation Host 与 Product
  Host 各自提供 adapter」——产品买方已在架构叙述中预期存在。

## Spec

1. **提升名单（冻结，仅可见性修饰符变化）**：`KernelRunDriver`、
   `RunDriverInputs`、`RunDriverInput`（含三个嵌套 record）、
   `AgentDecisionContext`、`AgentActionProposal`、`AgentDecision`
   （含其嵌套 cases——实现期逐成员核对，多公开一个即违规）、
   `RunDriveResult`、`RunDriveStatus`。
2. **零行为变化**：签名、逻辑、命名空间不动；仅 `internal` → `public`
   （含必要的成员可见性跟随）；注释中「internal 非公共契约」表述更新为
   「公开面，白名单执法」。
3. **白名单执法（DETERMINISTIC）**：Kernel.Tests 新增反射测试——
   `typeof(UniKernel).Assembly` 公开类型集合 == 既有公开集 ∪ 提升名单
   （名单字面冻结在测试里；未来任何新增公开类型 = RED，须走 change）。
4. **ADR**：关闭时落一页 ADR-0025（驱动面公开化 + 白名单执法 + 买方
   记录：SimulationHost 先例 / Product Host D8 裁决）。
5. Host 消费验证由 HOST-001 承接（本 change 不建 Host）。

## Out of Scope

- 签名/形状重构；驱动行为变化；IVT 收回（测试程序集现有内部访问维持）；
- Host 组合本体；freshness（FRS-008 线）。

## Acceptance

1. 提升名单外零新增公开类型（反射白名单 GREEN）
2. 全量回归零回归（基线以开工 HEAD 复跑为准）
3. Kernel diff 仅可见性/注释/新测试/ADR
4. KernelRunDriver 经公开 ctor 可由非 IVT 程序集构造（编译级证明 =
   HOST-001 解锁前提，本 change 以白名单+签名快照测试代证）

## Status log

- 2026-09-20 · created · HOST-001 D8 前置开立；决策面为空（纯机械 +
   已裁方向），直落 spec 待评审。
- 2026-09-20 · implemented·verified · 17 类型提升（spec 名单 16 +
   AgentPlanPolicy——ctor 参数为 internal 即 CS0050，随行提升）；构建零
  错误（无隐藏 internal 暴露）；`KernelRuntimeSurfaceWhitelistTests`
  冻结 196 项公开面字面白名单 GREEN；「internal 非公共契约」注释 13 处
  更新为 RUN-003 公开缝表述；ADR-0025 落档。零行为变化由构建 +
  KernelRunDriverTests 全绿佐证。
