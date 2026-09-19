# UniAgent Realization Architecture Baseline v0.1

> DocumentType: `UNIAGENT_REALIZATION_ARCHITECTURE_BASELINE_V0_1`
>
> Status: `FROZEN / PRODUCT-REALIZATION-BASELINE v0.1`（修订必须经 change）
>
> Authority: `PRODUCT ARCHITECTURE`（UniAgent realization 维度；继承 Target
> Product Architecture 与 Inter-Component Protocol，冲突时以上游为准）
>
> Date: 2026-09-12
>
> Provenance: UAR-001 candidate + Human H1 + SOL Standards/Spec PASS →
> UAR-002 Human adoption；不可逆取舍见 ADR-0019、ADR-0022。
>
> Forbidden Boundary: 本基线不定义 R1 activation/session contract，不授权实现，
> 不把 Host、Development Harness、模型、tool 或 transcript 提升为 Product
> Authority。

---

## 0. Purpose 与 Authority Order

本基线冻结 UniAgent 在 Codex 与 DeepSeek Harness（DSH）上的双 realization
架构契约。它回答“一个 Host realization 必须保持什么产品语义”，不回答“具体用
哪个 SDK、插件、transport 或部署拓扑实现”。

Authority order：

1. [Target Product Architecture Baseline](product-architecture-baseline-l0-l3.md)
2. [Inter-Component Protocol Baseline](protocols/inter-component-protocol-baseline-l1-l3.md)
3. [ADR-0019](../adr/0019-primary-run-is-self-driven-by-uni-kernel.md) 与
   [ADR-0022](../adr/0022-codex-and-dsh-are-dual-full-uniagent-realizations.md)
4. 本 UniAgent realization baseline
5. 具体 Codex/DSH realization、Adapter、配置与测试资产

低层 realization 不得覆盖高层 Owner、Authority、Lifecycle、Boundary、Invariant
或 protocol semantics。

## 1. Frozen Top-Level Shape

```text
                         Product User / Product Host
                                    │
                         UniAgent Conformance Surface
                                    │
                    ┌───────────────┴───────────────┐
                    │                               │
          Codex-backed UniAgent           DSH-backed UniAgent
          Simulation Realization           Product Realization
                    │                               │
             Codex Host Runtime               DSH Host Runtime
                    │                               │
                    └───────────────┬───────────────┘
                                    │
                         Product-semantic Kernel seam
                                    │
                                Uni Kernel
                                    │
                              Effect Boundary
                                    │
                               Environment
```

冻结决策：

- 两侧都是完整 UniAgent Realization，不是 UniAgent 内部 reasoning/model Adapter；
- Codex-backed 负责 Simulation Realization；
- DSH-backed 负责 Product Realization；
- 横向替换点是 UniAgent Conformance Surface，不是 Host transcript 或 tool loop；
- Host capability、模型能力、可定制程度或部署位置不产生 Product Authority。

## 2. UniAgent Realization Contract

UniAgent Realization 是在具体 Host Runtime 上完整承担 UniAgent 产品职责、遵守
Product lifecycle 并满足 Conformance Surface 的实现。每个 realization 必须：

1. 在 Product Session 内接收用户意图并维护显式关联；
2. 由 UniAgent 创建 Primary Goal 与 Goal Criteria；
3. author versioned Execution Contract Proposal，并只经 P1 交给 Run Model
   admission；
4. 消费 immutable P18 Runtime Outcome；
5. 由 UniAgent 形成 immutable Goal Evaluation；
6. 在缺少 required input/provider/mapping、结构不可解析或状态冲突时 fail closed；
7. 保持上游 cardinality：`1 Product Session / 1 Primary Goal / 1 Primary Run`。

Realization **不是**：

- 模型 Provider、prompt、单次 agent turn 或普通 AI Coding workflow；
- Uni Kernel driver、Capability Plane 或仅转发协议的浅 Adapter；
- Product Session、Primary Goal、Run State、WorldBelief、Assurance、Binding、
  Effect 或 Goal Evaluation 的平行 Authority。

## 3. 两种 Realization 的角色

### 3.1 Codex-backed UniAgent — Simulation Realization

Codex-backed UniAgent 用于产品语义模拟、场景验证、差分 conformance、failure /
recovery 场景与 DSH 回归对照。它是真实、非确定性的 UniAgent Realization，不是
deterministic fake；底层 Kernel fixture 可以是 deterministic fake/replay。

Simulation 角色不得：

- 降低本基线的 Owner/Authority/lifecycle/fail-closed 要求；
- 用 Codex coding task lifecycle 代替 Product lifecycle；
- 把文件、shell、Git 或代码修改能力默认带入 Product Runtime；
- 宣称 production availability、SLA、tenant isolation 或运营成熟度。

### 3.2 DSH-backed UniAgent — Product Realization

DSH-backed UniAgent 是目标产品 realization。它的内部 Profile/plugin/package
组合方式仍是 realization detail；无论如何组合，都必须满足完整 UniAgent
Realization Contract，不得复制 Product Owner、Authority 或 canonical state。

Product 角色不等于 production-ready。只有后续 evidence 证明 recovery、security、
isolation、audit、upgrade、operations 与真实环境行为后，才可增加 production-ready
声明。

## 4. Deep External Seam

UniAgent Conformance Surface 是两个 realization 共享的唯一外部测试/替换面。
调用者只需理解：

- Product Session correlation 与 user intent input；
- Product lifecycle state；
- Goal、Contract、Runtime Outcome、Goal Evaluation 的 canonical identity/version；
- typed failure、unavailable 与 fail-closed result；
- canonical Product records 与 evidence references。

调用者不得依赖：

- Codex thread/turn/item 或 DSH session/turn/step vocabulary；
- prompt/message 格式、context compaction、token budget；
- MCP、JSON-RPC、Cordis、Plugin、Profile 或 transport；
- Host retry、fork、approval、event 名称或 transcript layout。

具体 realization Adapter 位于此 seam 内部。Adapter 可以翻译 Host 事件与
Product records，但不得 author Primary Goal/Contract、重判 Runtime Outcome、形成
Goal Evaluation 或改变 completion semantics。

## 5. Product State 与 Host State

| Product concept | Host carrier relationship | Frozen rule |
|---|---|---|
| Product Session | 可与一个 Host Session 显式关联 | Host SessionId 不得隐式成为 Product SessionId |
| Primary Goal | 可由 Host 私有状态承载 projection | Host goal/status 不得成为 Primary Goal 或 satisfaction Authority |
| Execution Contract | realization 可承载 proposal | Host message 不是 Contract；只有 P1 admission 建立 Contract View |
| Primary Run | Host 可承载运行关联 | turn/step/round 不得冒充 Run 或 cycle |
| Runtime Outcome | Host 可传递 immutable envelope | Host 不重判、不改写、不补齐 |
| Goal Evaluation | Host 可持久化 canonical record | transcript/final answer 不是 Goal Evaluation |

所有映射必须显式、可恢复、可诊断。禁止：

- 根据 transcript 反向考古 canonical Product state；
- Host resume 成功即宣称 Product Session 或 Primary Run 恢复成功；
- Host fork 自动创建第二 Product Goal 或 Primary Run；
- Host event/log 因物理保存 Product records 而取得语义 Owner/Authority；
- 一个 Host failure/retry 产生第二条 lifecycle、Run activation 或 Effect path。

语义 Owner 与物理存储分离：存储位置只回答字节放在哪里，不改变谁有权创建、
修改或终止 canonical record。

## 6. Product Runtime 与 Development Harness 隔离

同一 Codex/DSH 可执行体可能被两个 Context 使用，但必须视为不同系统使用：

| Context | Canonical state | Instruction/tool set | Lifecycle / completion |
|---|---|---|---|
| Development Harness | Change State、Plan、WorkItem、代码与开发 Evidence | UniFlow、coding tools、repo skills | UniFlow Review/Verification/Completion |
| Product Runtime | Product Session、Goal、Contract、Run、Outcome、Evaluation | product instruction + explicit allowlist + Kernel seam | Product lifecycle + Goal Evaluation |

冻结隔离规则：

- Development WorkItem 不得成为 Primary Goal；
- UniFlow lifecycle 不得成为 Product lifecycle；
- AGENTS.md 或 coding Skills 不得成为 Product instruction authority；
- Development Session 与 Product Session 不得相互 resume；
- Product Goal Evaluation 不得被开发 Harness 的“任务完成”替代；
- Product Runtime 不得默认暴露 coding tools。

## 7. Inherited Kernel Supervision Boundary

Kernel driver、P1/P18 supervision arc、P1 Non-Activation 与 legal Primary Run
activation 的规范语义，唯一由
[ADR-0019](../adr/0019-primary-run-is-self-driven-by-uni-kernel.md) 和
[Inter-Component Protocol Baseline](protocols/inter-component-protocol-baseline-l1-l3.md)
拥有。本基线只要求所有 UniAgent Realization 遵守它们，不复制、不扩张或重新
解释其规则。Protocol Deferred ⑰ 仍未闭合，R1 未获授权。

## 8. Conformance Surface

### 8.1 必须一致

| Axis | Required conformance |
|---|---|
| Product cardinality | 1 Session / 1 Primary Goal / 1 Primary Run |
| Owner/Authority | 与 Target Product Architecture 完全一致 |
| Contract | author/admission/version/failure 与 Non-Activation 语义一致 |
| Runtime Outcome | immutable、exactly once、不可重判 |
| Goal Evaluation | immutable、幂等、不回写 Runtime truth |
| Terminal | terminal 后无新现实 Effect |
| Failure | 缺输入、缺 provider、不可解析、冲突时 fail closed |
| Correlation | Product ids 与 Host ids 显式映射、可恢复、无静默别名 |
| Evidence | canonical Product records 与 conformance evidence 可引用、可重放 |

### 8.2 允许不同

- 模型、prompt、context assembly；
- 内部 turn/step 数量和 Host event vocabulary；
- approval interaction、transport、persistence provider 与物理布局；
- latency、cost、token usage；
- 非语义自然语言措辞、simulation trace 与 production telemetry。

### 8.3 禁止作为 Conformance 判据

- transcript 逐字相等；
- tool-call sequence 逐步相等；
- Host thread/session id 相等；
- token、turn 或 step 数量相等；
- 只凭最终自然语言答案判断通过。

### 8.4 Minimum Scenarios

| # | Scenario | Required behavior |
|---|---|---|
| C1 | 相同用户意图，两 Host 正常执行 | 形成语义等价的 Goal/Contract/Evaluation records |
| C2 | Contract 非法或不完整 | admission fail closed；无 Primary Run |
| C3 | Host resume 成功、Product mapping 缺失 | Product Session 不恢复 |
| C4 | Host 重复投递 Runtime Outcome | 同输入幂等；无第二 canonical Evaluation |
| C5 | Kernel 已 terminal，Host 再调用工具 | 无新 Effect；无第二 Runtime Outcome |
| C6 | 模型只输出自然语言结果 | 不猜测 canonical record；显式 incomplete |
| C7 | required seam/provider 缺失 | session/run 不启动或 safe-stop |
| C8 | Host approval 被拒或 Host operation 被取消 | 无绕过路径；形成可观察的 fail-closed outcome，具体 lifecycle vocabulary deferred |
| C9 | Host fork | 不静默创建第二 Product Goal/Run |
| C10 | 两 realization 的非语义措辞不同 | canonical semantics 等价即可通过 |
| C11 | accepted 后重复提交同 version P1 | 返回同一 Contract View；零 activation/Run/Effect 副作用 |

## 9. Deferred 与禁止推断

以下内容未被冻结，也未获实现授权：

- Product Session identity algorithm、物理 persistence Owner 与恢复协议；
- Goal revision、multi-run、continuation、跨 Session identity；
- Deferred ⑰ legal Primary Run activation protocol；
- cancel/pause/resume/escalation command vocabulary 与 progress/event surface；
- Codex SDK/App Server 组合、DSH Profile/plugin/package 切分；
- Kernel Bridge transport、serialization、deployment、tenant、SLA 与 operations；
- 任何详细路线图、Tracer Bullet 或产品实现。

“Codex = Simulation”不得推断为 deterministic 或低标准；“DSH = Product”不得
推断为已经 production-ready；“Host 可持久化记录”不得推断为 Host 拥有记录。

## 10. Revision Rule

本基线自 UAR-002 起为 FROZEN。任何修改 realization role、Conformance Surface、
Host/Product identity 规则、Harness/Product 隔离或 Kernel supervision arc 的
change，都必须：

1. 明确真实 buyer 与被破坏的现有 invariant；
2. 判断是否需要 supersede ADR-0019 或 ADR-0022；
3. 保持上游 Target Product Architecture 与 Protocol Authority；
4. 独立 Review/Verify 后推进新版本。

设计来源与完整论证保留在
[UAR-001 design history](../design/uniagent-dual-realization-architecture-v0.1.md)。
