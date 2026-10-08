# PRF-002 — PRF-1b：产品 profile 与 DSH 绑定解耦

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

ADR-0041/设计文档切片 PRF-1b：产品 profile 出 `.dsh/` 与 adapter 程序集
（PRF-001 已上移类型），DSH 只持有绑定。产品 profile 获得模板化 host-neutral
定义（schemaVersion/identity/modelRoles/profileRevision）；模型角色必需性从
adapter 代码（G5）升为产品声明（Q4）。

## Scope

- 新建 `product/AGENTS.md` + `product/profiles/uniagent-prod.yaml`
  （schemaVersion/profileId/profileVersion/profileRevision/identity/
  capabilities/modelRoles；modelRoles key 必须是编译 LogicalProfileId 值域，
  未知 fail-closed）。
- 新建 `.dsh/product/uniagent-prod-bindings.yaml`：modelSelection+service
  整块自旧 yaml 迁移（CAP-007 语义原样；baseUrl 按工作树现状 3081——
  docs/agents/test-emulator.md 注册专线）。
- 拆双 loader：`UniClaw.Agent/Profile/UniAgentProfileYaml`（产品，含编译身份
  交叉核对自 UniagentProdYaml 迁入）+ `UniClaw.Agent.Dsh/
  UniagentDshBindingsYaml`（DSH 绑定）；共享 YAML 子集解析器
  （ProfileYamlDocument）上移 Agent。
- `UniagentProdConfiguration` 退役（合并两 owner 数据的组合记录=重建耦合）；
  `DshModelManagement.FromProfile` → `FromBindings(bindings, profile,
  override)`，Q4 语义：required 角色吃 selected 缺省（loader 保证可解析，
  装配不可能带未解析 required 完成装配）；optional 无显式选择 → 诚实
  NotConfigured；绑定引用产品未声明角色 → fail-closed。
- env 双轨：`UNICLAW_UNIAGENT_PROD_CONFIG`（产品）/
  `UNICLAW_UNIAGENT_DSH_BINDINGS`（DSH 绑定）。
- 删除旧 `.dsh/profiles/uniagent-prod.yaml` 与 `UniagentProdYaml`；
  `UniagentProdConfiguration` 自 ProtocolModels 移除。
- 组合根（Host.Dsh Program/RuntimeHttpServer）与测试跟随
  （DshModelManagementTests 全套迁移、ObserverProjection 三测试重写、
  Agent.Tests 新增 UniAgentProfileYamlTests 五用例）。
- 职责基线（repository-role-boundaries-v0.1）增 `product/` 行（经本 change）。
- 顺带落定：ObserverProjection 陈旧断言 3080→3081（test-emulator.md 注册
  值；原属并行 CAP 会话的 yaml 中间态，随文件迁移一并收口）。

## Out of Scope

- prompt manifest/policy 升格/revision 执法/envelope（PRF-2/4/5）。
- 任务 profile 搬迁（第二批）。
- Kernel/协议/schema/hash 任何变更。

## Decisions

1. modelRoles 用块状 YAML（子集解析器不支持 flow map）；值域=LogicalProfileId
   三静态属性（agent.decision/slow.semantic.text/slow.semantic.visual）。
2. profileRevision 声明位=1 起步；执法（钉扎/envelope）在 PRF-005（Q8）。
3. 绑定引用未声明角色 fail-closed（防死配置）；required 无显式选择吃
   selected 缺省（现状 agent.decision/text 行为），optional 无显式选择不注册
   （现状 visual 行为）。装配后 required 可解析性由 loader 缺省链保证
   （choices 非空+selected 必解析），不加不可达的二次守卫。

## Acceptance

1. 产品 profile 文件无 provider 名/baseUrl；`.dsh/product/` 绑定文件存在且
   旧 yaml 已删。
2. 产品 loader：身份漂移/未知角色/缺 required 字段/schemaVersion 漂移
   fail-closed 用例过。
3. FromBindings：未声明角色引用拒绝、CAP-007 偏好序/env 覆盖语义测试全过。
4. 全解测试绿（scenario 源哈希随 src 变更经 certify --change PRF-002 重封）。
5. 职责基线含 product/ 行。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | 产品 profile 内容边界 | 无 provider/baseUrl 值（命中仅注释） | PASS | `product/profiles/uniagent-prod.yaml`（全文 34 行，唯二命中为头注释与 schemaVersion 行） |
| CONTRACT | 文件存在/删除 | `.dsh/product/` 绑定在；旧 yaml 删 | PASS | `ls .dsh/product/ .dsh/profiles/` |
| DETERMINISTIC | UniAgentProfileYamlTests（Agent.Tests） | 漂移×2/未知角色/坏 required/仓库加载 五用例过 | PASS；Agent.Tests 22/22 | `tests/UniClaw.Agent.Tests/UniAgentProfileYamlTests.cs` |
| DETERMINISTIC | DshModelManagementTests 迁移 + ObserverProjection 重写 | 未声明角色拒绝/偏好序/override/NotConfigured/3081 断言全过 | PASS；Agent.Dsh 158/158 | `tests/UniClaw.Agent.Dsh.Tests/DshModelManagementTests.cs`；`ObserverProjectionTests.cs` |
| SCENARIO | 全解 `dotnet test UniClaw.Kernel.slnx --no-restore` + certify | 全绿 0 违规 | **PASS 1456/1456**（此前 3081 外部失败一并落定）；`--check`=PASS(28,0)，20 块 --change PRF-002 重封 | 全解输出；`tools/scenario_certify.py` |
| CONTRACT | 基线/README 登记 | product/ 行 + Profile/ 与绑定文件登记 | PASS（Kernel.Tests 849/849 含 ownership） | `docs/architecture/repository-role-boundaries-v0.1.md`；两个 README |

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 消费面核对
  （Program/RuntimeHttpServer/DshModelManagementTests×26/ObserverProjection）；
  3081=注册专线（test-emulator.md），并行会话中间态随迁移落定。
- 2026-10-07 · IMPLEMENT → REVIEW → VERIFY → CLOSED · 双文件+双 loader+双 env
  落地；UniagentProdConfiguration/UniagentProdYaml 退役；测试迁移+新增；
  scenario 重封 20 块（PRF-002）；基线增 product/ 行；全解 1456/1456 绿。
