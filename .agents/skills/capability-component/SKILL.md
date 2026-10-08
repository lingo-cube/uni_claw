---
name: capability-component
description: UniClaw capability component development discipline. Load before building or integrating ANY component that will be declared in a Capability Registry (perception, model management, effect provider, inspector, measurement, sink, fixture...), before declaring capabilities, or when auditing an existing capability's conformance. Enforces the ICapability inheritance ladder (L0-L3), protocol-per-capability with registry enforcement, buyer-first gating, the new-capability detail-grill gate (granularity/allowlist/expectation-source/violation-boundary/host-hook questions BEFORE implementation), executable instance registration, quality gates (single responsibility, cohesion, pluggability, health probe duty), lifecycle rules mapped to .NET disposal mechanics, and the repo's enforcement points (public-surface whitelist, closure tests, scenario recertification). HOW-only; canonical truth stays in the referenced docs.
source: LOCAL_UNICLAW
---

# Capability Component（能力组件开发纪律）

## 判断门（先过这关）

1. 组件**不进 Capability Registry**（纯 Host 管线组件）→ **不适用**本 skill；
   走 `codebase-design` 深模块纪律即可。
2. 组件**要进 Registry** → 先**分流**（所有者裁决点，不可自行判定）：

   **路径 A【既有能力的延伸/替换实现】**：协议与词汇已冻结（如感知
   realization 替换、模型管理换供货方）。直接从第 4 步（Implementation）
   进入；R5 可替换性测试是核心验收；不得顺手改协议形状。

   **路径 B【全新能力】**：不存在已冻结的协议/词汇。必须依次完成：
   买方五问（第 1 步）→ **细节拷问门**（见下专节）→ 才允许进入第 2 步
   （Definition）。未过拷问门的"实现完成"不算完成——语义假设缺陷的
   返工成本远高于提问成本（CAP-012 教训，2026-10-06 所有者纠偏后立此门）。

## 细节拷问门（路径 B 必经的常设机制）

**时机**：买方五问之后、Definition（第 2 步）之前。任何领域语义设计决策
不得由实现方自行拍板后"先做再问"。

**发问协议**（组合 `grilling` skill 的轮次纪律）：

- 把当前**前沿**（前置已定、现在就能问的问题）**一轮问全**：逐题编号、
  附**推荐答案**及理由，然后**等所有者作答**；
- 某个答案改变了决策树 → 重算前沿再问下一轮，不猜未听到的答案；
- 事实问题（代码/文档里查得到）自己查，不问所有者。

**五类必问**（每类至少一问；示例仅示形状，按能力域裁剪）：

| 类 | 要问清什么 | 示例问题 |
|---|---|---|
| ①作用粒度 | 判定的最小可解释单位 | 菜单级 / 菜单项级 / 文本项级 / 可配置聚合？违例定位到哪一层？ |
| ②豁免/白名单 | 已知合法例外怎么表达 | 要不要白名单？挂全局词条 / per-menu / per-item？词条还是模式？谁维护、入版本控制吗？ |
| ③期望值来源 | 判定依据从哪来、冲突听谁的 | 配置声明 / 设备环境实测 / 逐目标声明？声明与实测不一致时以谁为准、差异要不要报？ |
| ④违例处置 | 结果的权威边界 | 非权威 Finding 还是影响判定？阻断边界在哪？要进判定须立 Promotion 吗？ |
| ⑤宿主集成点 | 在哪跑、何时跑 | 哪个 host？哪个生命周期钩位（同步 / post-commit）？本轮接线还是能力就绪集成另立 change？ |

**按域裁剪**：从协议选择表（开发协议指南 §3）推导域特有问题补入前沿。
例：感知类必问 capture/cycle 关联与新鲜度要求；模型管理类必问候选集与降级
轨迹表达（CAP-006/007 先例）；检查类必问粒度与豁免（本门起源域）。

**记录纪律**：每条裁决落 change state 的 **Decisions 表**（编号 + 日期 +
所有者标识）；实现期发现**未问过的语义分叉** → 停下、回 RESOLVE 补问，
不得就地拍板（uniflow A7 语义缺陷边的预防形式）。

**完成判据**：前沿为空（所有领域语义问题都有所有者答案并落档）→ 才可进
第 2 步 Definition；协议与词汇按裁决冻结，后续变更走 R6 版本纪律。

## 接口继承位势（严格顺序，不可跳层）

```text
L0  ICapability                        根接口，只暴露不可变 Description（CAP-002 冻结）
L1  协议能力接口 : ICapability          每个产品能力 Definition 一个，Kernel/Capability/
                                        协议常量同点（PerceptionProtocol /
                                        ModelManagementProtocol 先例）；公开=白名单修订
L2  产品实现类 : L1接口                  产品拥有语义与执法（解析/fail-closed/健康聚合）
L3  realization / adapter              永不实现 L1！只经缝注入数据（Register 候选、
                                        ApplyHealth 证据）；provider 词汇不出 adapter 层
```

健康探测是**可选能力面**（mixin）：`ICapabilityHealthCheckable.CheckHealth()`
（拉式只读；外部可观测可用性的能力必须实现——R7）。

运行剖面是**第三个可选能力面**：`ICapabilityProfileReporting.DescribeProfile()`
→ `CapabilityProfileReport`（Summary / 生效配置 / 影响披露 / Limitations）。
回答"我在什么配置下运行、缺了什么、对结果有什么影响"——缺配置类豁免的
缺省行为是**照跑 + 披露**（D8 先例：不把缺词表放大成能力不可用）。

## 开发七步（每步带执法）

1. **买方**：五问——谁消费/输入/输出/是否动主权威/失败策略（docs/capability-hub/
   customization-integration-development-protocol-v0.1.md §2）。无买方即停。
   路径 B（全新能力）在五问后必须过**细节拷问门**（见专节）才可进第 2 步。
2. **Definition**（Kernel）：L1 接口 + 协议常量 + L2 实现类。
   **执法**：`KernelRuntimeSurfaceWhitelistTests` 必须同 change 修订（公开面扩张
   = 显式声明，HOST-001 D8）。
3. **Declaration**（组合根）：CapabilityDescription 组装（正确信任域 + scope +
   协议 + realization 角色显式声明"借用谁"）。
   **执法**：`CapabilityRegistry.ValidateDescription/ValidateImplementation`
   （协议↔接口一致性、scope 越域、重复注册、非法迁移全 fail-closed）。
4. **Implementation**（adapter 层）：按 provider 实现注入；配置源单一
   （如 UniagentProdYaml）；fail-closed 校验。
   **执法**：闭包测试（`DecisionChannelClosureTests` 禁词扫描 / 
   `ProductHostClosureTests` 依赖闭包）。
5. **Binding**（组合根）：**注册可执行实例** `Register(instance, source)`——
   声明与运行时同一事实，`Resolve(id)` 可取回。R1：可执行能力必须实例注册；
   description-only 仅限正当场景且 change 必须记理由（ModelRouting 类别
   直接拒绝 description-only）。
   **执法**：实例注册往返测试（Resolve 取回 + 无状态分叉）。
6. **测试**：确定性契约测试（含 R5 可替换性执法：同一 L1 接口消费闭包喂
   两个 realization 实例，行为契约互换）。Kernel 源变更后必须
   `python3 tools/scenario_certify.py --change <id> --all` 再认证。
7. **记录**：`changes/<id>/state.md`（四元组验证）+ evidence + INDEX 再生。

## 协议质量门（每个协议冻结前逐条过）

- **R4 每能力一协议**：L1 接口必须有自己的协议（名称+版本）且 registry 校验
  协议-接口一致性（感知与 ModelRouting 均有执法先例）。
- **职责单一**：一个协议只回答一个问题；禁止万能 Hook。
- **高内聚低耦合**：协议词汇不跨能力引用；Definition 层零 provider/model 名。
- **可替换可插拔**：同协议 realization 替换只动 adapter+组合根，L2/消费方零改动
  ——必须写成确定性测试，不是口头承诺。
- **R6 版本纪律**：向后兼容扩展不升版本；语义变更必须升版本并走 change 冻结。
- **R7 健康义务**：外部可观测可用性的能力必须实现 ICapabilityHealthCheckable；
  纯计算能力可豁免（恒 Healthy 是仪式）。

## 生命周期规则（RL1-RL3：管理面事实 × .NET 资源面机制）

| Hub 状态（事实，不驱动） | .NET 机制（资源） | 驱动者 |
|---|---|---|
| Declared→Registered | 无（注册即事实） | 组合根 `Register(实例)` |
| Registered→Ready→Active | 无自动驱动；有真实启动动作时显式 `Commit` | 组合根 |
| Active→Draining→Closed | `IAsyncDisposable.DisposeAsync()`（幂等） | 组合根 `finally` |

- **RL1**：持资源能力（网络 peer/进程/句柄）必须实现 IAsyncDisposable 并走完整
  事实链；纯内存常驻能力（如 ModelManagement）声明"Registered 即稳态、无
  teardown 义务"记入 change。
- **RL2**：**Hub 永不调用 Dispose**——事实记录，不驱动（ADR-0035"Teardown 独立
  于 Hub 通知且幂等"；防万能回调）。
- **RL3**：不引入 IHostedService / IServiceCollection / DI 扫描——显式组合是根
  纪律（Agent.Dsh 闭包禁词执法；扩面到 Kernel/Host 属可选执法项，未立项）。

## 有意留白清单（勿当缺陷修补；补任一项须新 change）

| 留白 | 出处 |
|---|---|
| Hub 事实 push 订阅/分发/背压 | ADR-0035"第一版只实现被动接缝" |
| typed Resolve（泛型取回） | CAP-002 冻结 ICapabilityHub 形状；无买方 |
| task-scoped capability binding API | CAP-005 设计已出、API 明确暂缓 |
| DSH Tool/Skill 投影进 Hub | ADR-0038：DSH Tool 是 Binding/Exposure 非能力类型 |
| 自动注册禁词的 Kernel/Host 执法 | 本 skill RL3 条款；扩面待所有者立项 |
| capability 级运行期健康事实上报 | ADR-0035：owner 在提交点产生；当前无 owner 产生 |

## 真相源索引

- 买方五问 / 协议选择表 / 感知特殊规则：`docs/capability-hub/customization-integration-development-protocol-v0.1.md`
- 能力类别与接缝：`docs/design/runtime-capability-integration-seams-v0.1.md` §4
- 三层边界（Definition/Implementation/Binding）：`docs/adr/0038-capability-definition-implementation-binding-boundary.md`
- 信任域注册：`docs/adr/0035-capability-management-hub-trust-scoped-registries.md`
- 管理面骨架：`src/UniClaw.Kernel/Capability/CapabilityHub.cs` + `src/UniClaw.Kernel/Capability/README.md`
- worked example：Model Management 声明与完备化（changes/CAP-006、CAP-007、CAP-008）、
  感知实例化与健康聚合（changes/CAP-009）、Language Inspection
  （changes/CAP-012：Runtime Integration 域独立注册表先例；其初版跳过拷问门被
  所有者纠偏重开——**细节拷问门的教学案例**，裁决记录见其 Decisions 表）

## 已知偏差（如实记录，勿默许扩散）

- `slow.visual` 仍为 description-only 注册（visual 未接线，无运行时实例）——
  CAP-009 known gap。
- `fast.yolo` / `fast.ocr` 为声明性依赖锚点（description-only，理由：确定性本地
  资产、无实例语义；协议词汇待独立 change 冻结）——CAP-009 记录。
- 感知协议负载词汇（SemanticObservationProposal / PerceptionAssessment）未实现
  ——uni.perception 的 L2（UniPerceptionCapability）当前实现双协议 marker +
  健康聚合；负载词汇由下一个感知买方驱动冻结。

## 剖面消费（agent 表达模板，D9 裁决）

剖面的消费面是 **uni agent**：能力只供结构化事实（run 落盘 `capability-profiles.json`
为事实源）；表达边界与范围由 agent 用模板完成——事实归能力、表达归 agent，
agent 不得编造事实层没有的语义。模板（agent 读到 profile 后按此向用户表达）：

```text
【能力】{Summary}
【当前生效】逐条 EffectiveConfiguration（含"未加载/未配置"状态，照实说）
【影响】逐条 ImpactDisclosures：在 {Condition} 下，{Impact}
【边界】Limitations（已知不做的事）+ 依赖（Description.Dependencies）
```

要点：①"没配 X 会怎样"是用户最常问的问题——ImpactDisclosures 必须覆盖已知
缺省项；②能力初期描述不可能详尽（所有者原话"不一定能在一开始就描写得很
详细"）——模板引导 agent 从事实外推边界表述，但外推要标注"据配置推断"；
③记忆可用，但结论以落盘事实源为准。
