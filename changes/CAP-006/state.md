# CAP-006 — Model Management 产品能力组件声明（缺省 DSH realization）

lifecycle_state: closed · disposition: none · depth: standard · base: 84a3abc2

## Intent（WHAT/WHY）

所有者指令（2026-10-06）："模型管理功能目前依托 DSH；产品本身应拥有此能力，暂时借用 DSH 的配置/组件/接口实现，但必须声明一个自己的能力组件。"

现状事实（已核实）：

1. Kernel 已有窄缝 `SlowModelManagement`（internal，Perception）：logical profile → 冻结
   `ModelBindingSnapshot` 解析，fail-closed（`ROUTING_UNAVAILABLE`）。它已有 3 个真实
   realization（deterministic replay / OpenCode audit / DSH 事实绑定），但 internal 且仅
   `SlowConsultation`（P2 路径）经过它。
2. live 真件路径的模型管理实际由 DSH 侧承担：`ModelConfiguration(Provider, Name)`
   （Agent.Dsh）+ `.dsh/profiles/uniagent-prod.yaml` `modelSelection` 块；组合根
   （Host.Dsh/Program.cs）把 yaml 模型直连喂给 `DshOpenedHttpPeer`（决策）与
   `DshSlowConsult`（slow text），完全绕过产品缝。
3. 管理面无声明：CapabilityRegistry 已注册 `uni.perception` / `slow.visual`，模型管理
   无 descriptor。

所有者已裁决（2026-10-06 ask_user_question）：

- 落地深度 = **声明 + 改走缝**：缝公开化为产品能力组件，DSH adapter 注册 binding，
  组合根模型一律从缝 resolve；声明与实际运行路径一致。
- 覆盖范围 = **含 agent 决策模型**：logical profile 增加 `agent.decision`（yaml 的
  `modelSelection.selected` 本来就在管它，是今天真实的模型管理买方）。

## Scope / Out of scope

### 范围

- Kernel：新公开能力组件 `UniClaw.Kernel.Capability.ModelManagement`
  （`LogicalProfileId`（+`AgentDecision`）/ `ModelBindingSnapshot` / `ModelBindingResolution`
  / `ModelRoutingStatus` / `ModelManagement`），语义自现有 internal 缝原样冻结
  （Register/Resolve/IsAvailable；不下载、不安装、不热切换；fail-closed 词汇不变）。
- Kernel：Perception 内部 `SlowModelManagement`/`LogicalProfileId`/`ModelBindingSnapshot`/
  `SlowBindingResolution` 去重，6 处 internal 引用切到公开类型；`SlowReplayProfiles`
  （deterministic 缺省）保持 internal。
- Kernel 公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）显式增集 5 个公开类型。
- Host：`ModelManagementCapabilityComposition` 注册 `uni.model.management` descriptor
  （ProductRuntime scope，协议 Model Binding Resolution@1.0，realization 角色缺省
  `dsh-model-management`——把"暂时借用 DSH"声明为显式事实）。
- Agent.Dsh：`DshModelManagement`（yaml `modelSelection` → 产品缝 binding 注册 +
  决策模型 → DSH `ModelConfiguration` 转换；DSH 专有类型不越出 adapter）。
- Host.Dsh/Program.cs：决策 peer 与 slow bridge 模型改从缝 resolve；管理面声明并入
  capability-facts 落盘。配置面不动（`modelSelection` 原样复用，不新增 per-profile 键）。

### 不在范围

- 不引入 `IModelManagement` 接口形态：解析/fail-closed 语义归产品所有，可替换的是
  binding 来源（realization 注册），不是解析语义本身。
- 不新增 yaml per-profile 模型选择键（无真实买方，ADR-0026 不预造）。
- 不改 `SlowConsultation`/P2/orchestration 语义；不动 InternalsVisibleTo 纪律
  （公开化不新增 internal 可见性）。
- 不实现模型下载/安装/热切换（缝的既有边界）。

## Decisions

| # | 决策 | 理由 |
|---|---|---|
| D1 | 缝=注册式（Register binding），非接口实现式 | fail-closed 解析语义留在产品；DSH 只供 binding 数据；替换 realization 不改核心 |
| D2 | `agent.decision` + `slow.semantic.text` 共用 yaml `selected`（现状行为），visual 不注册（诚实 NotConfigured） | 配置面零扩张；与现 live 行为逐位等价 |
| D3 | descriptor 的 realization 角色声明缺省 `dsh-model-management`，落在 Host 组合根（Binding 层），Kernel 只拥有 Definition | ADR-0038 三层边界；DSH 词汇不进 Kernel |
| D4 | 组件位置 `Kernel/Capability/ModelManagement.cs`，非 Perception | 模型管理是跨感知/决策的产品能力，Perception 只是消费方之一 |

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | 公开缝语义冻结 | Kernel 现有感知测试全绿（类型迁移零语义漂移）；新增 ModelManagement 契约测试（AgentDecision profile 解析/fail-closed） |
| A2 | 白名单执法同步 | KernelRuntimeSurfaceWhitelistTests 增集 5 类型后全绿（公开面扩张是显式声明） |
| A3 | DSH 缺省 realization | DshModelManagementTests：yaml selected → agent.decision + slow.text 注册；override 透传；ToDshModel 转换；visual 诚实 RoutingUnavailable |
| A4 | 声明入管理面 | ModelManagementCapabilityCompositionTests：descriptor + dsh realization 角色 + lifecycle fact |
| A5 | 组合根改线 | Program.cs 决策/slow 模型均经缝 resolve；`dotnet build` 零警告；全量测试绿 |

## Verification（2026-10-06 回填）

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `dotnet build UniClaw.Kernel.slnx` | 0 error | 0 error（0 个新增警告） | 本地构建输出 |
| DETERMINISTIC | Kernel.Tests 803（含白名单执法 + ModelManagementTests 5 新例） | 全绿 | PASS 803/803 | `dotnet test tests/UniClaw.Kernel.Tests` |
| DETERMINISTIC | Host.Tests 170（含 ModelManagementCapabilityCompositionTests 5 新例） | 全绿 | PASS 170/170 | `dotnet test tests/UniClaw.Host.Tests` |
| DETERMINISTIC | Agent.Dsh.Tests（含 DshModelManagementTests 5 新例） | 全绿 | PASS 144/145：唯一失败 `ObserverProjectionTests.Runtime_Configuration...` 为并行 PER-019 会话的 yaml 中间态（`selected: deepseekFlash` 无 choices 条目，config-missing 于 UniagentProdYaml 加载路径），与 CAP-006 无关（本 change 未触 yaml/Yaml 解析） | `dotnet test tests/UniClaw.Agent.Dsh.Tests` |
| DETERMINISTIC | 场景源码哈希执法（Kernel 源变更触发） | 再认证后全绿 | `tools/scenario_certify.py --change CAP-006 --all` 写 20/28 认证块后 Simulation.Tests PASS 188/188 | `dotnet test tests/UniClaw.Simulation.Tests` |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 事实链核实（缝在但 internal、live 路径绕缝直连 yaml、管理面无声明）；所有者裁决深度/范围；开工。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY · 落地：Kernel 公开缝（5 型 + AgentDecision 值域）→ Perception 6 文件去重切换 → 白名单增集 5 型 → Host 声明组合 → Agent.Dsh 缺省 realization → Program.cs/RuntimeHttpServer 改线；场景哈希再认证 20 块；验证四元组见上。与并行 PER-019 会话共享 Program.cs（改动区域不相交，合并态已复核）。文档同步：capability-hub README Product Registry 域描述。
- 待所有者验收后 CLOSED（Agent.Dsh 套件的 yaml 中间态失败归属并行会话收尾）。
- 2026-10-06 · CLOSED（所有者验收，2026-10-06）· 验收基线：全量测试绿（最终核验 1372/1372、build 0 error，含 CAP-007/008 叠加后的回归）；范围完成、acceptance A1-A5 均有四元组证据、无未授权改动、文档已同步（capability-hub README + 白名单）。当时记录的 yaml 中间态失败已由 CAP-007 附带同步解决。后续深化见 CAP-007（选择能力）与 CAP-008（可执行契约）。
