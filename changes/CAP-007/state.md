# CAP-007 — Model Management 选择能力完备化（per-profile 选择 / 健康审计 / 候选偏好）

lifecycle_state: closed · disposition: none · depth: standard · base: 63804f7a

## Intent（WHAT/WHY）

所有者指令（2026-10-06，CAP-006 交付后追问）："新管理的 capability 是否能完备地成为
可插拔组件、提供很好的模型选择能力供整个服务使用、并很好地替换？"——评估后所有者
裁决："上述（缺口）都有必要"。

CAP-006 交付的缝是"解析器"（每 profile 单 binding、静态健康、无候选序）。本 change
按所有者指令补齐为"选择器"，三块能力：

1. **per-profile 选择**：配置可按产品 logical profile 各自选模型（决策与 slow text
   可不同），不再共用单一 selected。
2. **健康审计协议**：缝上通用 ApplyHealth——realization 可在运行时按
   (profile, provider, model) 上报健康证据，更新候选可用性（一般化 OpenCode audit
   先例："A string in a config file is never sufficient"）。
3. **候选偏好 + fallback 链**：每 profile 可注册有序候选；Resolve 依序游走，首个
   可用者胜出；跳过时在解析快照上标记 FallbackFrom（诚实降级轨迹）。

第 4 项缺口（非 DSH 决策通道 transport realization）**不在本 change**：需要先点名
具体目标 provider（provider 绑定只在 adapter），无目标无法实现与验证。

## Scope / Out of scope

### 范围

- Kernel `ModelManagement`：存储改为每 profile 有序候选（并发安全）；新增
  `RegisterPreference(profile, orderedCandidates)` 与
  `ApplyHealth(profile, providerId, modelId, healthy)`；`Resolve` 依序游走（显式
  explicitFallback 语义保持不变；全部候选不可用 → RoutingUnavailable，诊断列出
  tried 候选）；`Register` 保持"设为唯一候选"语义（兼容）。
- Agent.Dsh 配置面：yaml `modelSelection.profiles` 块（profile → choice key 标量或
  有序列表）；loader fail-closed 校验引用的 choice 必须存在；
  `UniagentProdConfiguration` 保留完整 choices 映射 + ProfileSelections。
- `DshModelManagement.FromProfile`：per-profile 显式选择 → RegisterPreference；
  未显式 profile 维持 selected 缺省（agent.decision + slow.semantic.text；
  visual 缺省仍不注册——但可显式配置，新能力）；未知 profile 名 fail-closed。
- 环境覆盖 `UNICLAW_UNIAGENT_PROD_MODEL` 优先级定义：只作用于 selected 派生的
  缺省 binding；显式 per-profile 选择是明确决策，不被覆盖。
- 确定性测试：Kernel 候选/健康契约；Agent.Dsh fixture yaml 解析与映射。

### 不在范围

- 非 DSH 决策通道 transport（前置：点名 provider）。
- 自动健康探测调度（谁在何时调 ApplyHealth 属组合根/realization 责任；缝只提供协议）。
- 不改 CAP-006 冻结的 fail-closed 词汇与 explicitFallback 既有语义。
- 不动管理面 descriptor（协议仍为 Model Binding Resolution@1.0，候选/健康是其
  向后兼容扩展）。

## Decisions

| # | 决策 | 理由 |
|---|---|---|
| D1 | 候选存于缝（产品）而非 adapter | 偏好序与降级轨迹（FallbackFrom）是产品语义；adapter 只供候选数据 |
| D2 | ApplyHealth 按 (profile, provider, model) 精确匹配，bool 返回 | 健康是外部证据事实；匹配不到 = no-op 诚实返回 false，不抛（后台审计不该崩） |
| D3 | env 覆盖不作用显式 per-profile 选择 | 显式选择是更强决策；覆盖缺省即可满足开发期换模型便利 |
| D4 | yaml 标量=单选、列表=有序偏好 | 复用 mini YAML 既有两种值形态，零语法扩张 |

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | 候选偏好 | Kernel 契约测试：首可用胜出 / 跳过标记 FallbackFrom / 全不可用 honest RoutingUnavailable（列 tried）/ 非法注册（空、跨 profile、重复身份）fail-closed |
| A2 | 健康协议 | Kernel 契约测试：ApplyHealth 翻转候选可用性并影响 Resolve；未知身份 false；恢复路径 |
| A3 | 兼容 | 既有全部测试（Kernel 803 + Host 170 + Agent.Dsh）保持绿；explicitFallback 语义不变 |
| A4 | per-profile 配置 | fixture yaml 测试：标量/列表映射、ConfigId 逐候选、未知 choice（loader）/未知 profile（FromProfile）fail-closed、visual 可显式配置、env 覆盖优先级 |
| A5 | 全量 | build 0 error；场景哈希再认证后全绿 |

## Verification（2026-10-06 回填）

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `dotnet build UniClaw.Kernel.slnx` | 0 error | 0 error（0 新增警告；Kernel 公开面无新类型，白名单不变） | 本地构建输出 |
| DETERMINISTIC | Kernel.Tests（ModelManagementTests 8 新例：偏好序游走/FallbackFrom 轨迹/tried 诊断/非法注册 fail-closed/健康翻转与恢复/未知身份 no-op/全候选摘除） | 全绿 | PASS 811/811 | `dotnet test tests/UniClaw.Kernel.Tests` |
| DETERMINISTIC | Agent.Dsh.Tests（DshModelManagementTests 8 新例：per-profile 覆盖与偏好序/override 只作用缺省/visual 显式注册/未知与重复 profile fail-closed/Choices 缺失 fail-closed；fixture yaml 3 例：标量+列表解析进偏好、未知 choice 与残缺条目 load 时 fail-closed） | 全绿 | PASS 153/153 | `dotnet test tests/UniClaw.Agent.Dsh.Tests` |
| DETERMINISTIC | Host.Tests（声明组合回归） | 全绿 | PASS 170/170 | `dotnet test tests/UniClaw.Host.Tests` |
| DETERMINISTIC | 场景哈希执法（Kernel 源再次变更） | 再认证后全绿 | `scenario_certify.py --change CAP-007 --all` 刷 20 块后 PASS 188/188（首跑 `AsyncTraceWriterTests.SlowWriter_DoesNotBlockRuntime` 时序抖动，单跑+整套复跑均绿，与本 change 无关的 Trace 模块） | `dotnet test tests/UniClaw.Simulation.Tests` |
| DETERMINISTIC | 兼容面（A3） | explicitFallback 语义与 CAP-006 冻结行为不变 | 既有全部测试保持绿（含 SlowReplayRealizationTests 的 fallback 断言原样通过） | 同上 |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 所有者裁决"缺口都有必要"；OpenCode audit 消费面核实（仅测试，无生产调用方——协议做在缝上，wiring 留给未来组合根）；开工。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY · 落地：Kernel 缝候选化（ConcurrentDictionary 快照替换 + RegisterPreference + ApplyHealth + Resolve 依序游走）；Agent.Dsh 配置面（choices 目录保留 + profiles 块标量/列表 + fail-closed 校验）与 FromProfile 映射（D3 override 优先级）；测试 16 新例。跨会话事实：并行 PER-019 会话已提交 63804f7a（携带 CAP-006）；其 yaml 默认模型切换（deepseek 直连）漏改 ObserverProjectionTests 陈旧断言，本 change 附带同步（注释标明归属）。验证四元组见上。第 4 项缺口（非 DSH 决策 transport）待点名 provider，独立 change。
- 待所有者验收后 CLOSED。
- 2026-10-06 · CLOSED（所有者验收，2026-10-06）· 验收基线：全量测试绿（最终核验 1372/1372、build 0 error）；acceptance A1-A5 均有四元组证据；场景哈希已按 CAP-007 再认证。第 4 项缺口（非 DSH 决策 transport）如 out-of-scope 记录，待 provider 点名另立 change。可执行契约对齐由 CAP-008 承接。
