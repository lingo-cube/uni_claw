---
name: capability-component
description: UniClaw capability component development discipline. Load before building or integrating ANY component that will be declared in a Capability Registry (perception, model management, effect provider, inspector, measurement, sink, fixture...), before declaring capabilities, or when auditing an existing capability's conformance. Enforces the ICapability inheritance ladder (L0-L3), protocol-per-capability with registry enforcement, buyer-first gating, executable instance registration, quality gates (single responsibility, cohesion, pluggability, health probe duty), lifecycle rules mapped to .NET disposal mechanics, and the repo's enforcement points (public-surface whitelist, closure tests, scenario recertification). HOW-only; canonical truth stays in the referenced docs.
source: LOCAL_UNICLAW
---

# Capability Component（能力组件开发纪律）

## 判断门（先过这关）

- 组件**要进 Capability Registry**（被声明、被解析、有生命周期事实）→ 适用本 skill，继续。
- 不进 Registry 的纯 Host 管线组件 → **不适用**；走 `codebase-design` 深模块纪律即可。
- 不确定 → 回答买方五问（见第 1 步）；答不出"谁消费结果"就没有能力，只有代码。

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

## 开发七步（每步带执法）

1. **买方**：五问——谁消费/输入/输出/是否动主权威/失败策略（docs/capability-hub/
   customization-integration-development-protocol-v0.1.md §2）。无买方即停。
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
  感知实例化与健康聚合（changes/CAP-009）、**全新能力端到端**（changes/CAP-012
  Language Inspection——本 skill 首次从零按七步实战，含 Runtime Integration
  域独立注册表先例）

## 已知偏差（如实记录，勿默许扩散）

- `slow.visual` 仍为 description-only 注册（visual 未接线，无运行时实例）——
  CAP-009 known gap。
- `fast.yolo` / `fast.ocr` 为声明性依赖锚点（description-only，理由：确定性本地
  资产、无实例语义；协议词汇待独立 change 冻结）——CAP-009 记录。
- 感知协议负载词汇（SemanticObservationProposal / PerceptionAssessment）未实现
  ——uni.perception 的 L2（UniPerceptionCapability）当前实现双协议 marker +
  健康聚合；负载词汇由下一个感知买方驱动冻结。
