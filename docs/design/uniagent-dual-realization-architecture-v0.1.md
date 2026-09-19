# UniAgent Dual-Realization Architecture v0.1

> DocumentType: `UNIAGENT_DUAL_REALIZATION_ARCHITECTURE_V0_1`
>
> Status: `CANDIDATE / SUPERSEDED_BY_FROZEN_BASELINE / UAR-001_CLOSED / R1_NOT_AUTHORIZED`
>
> Authority: `NONE`
>
> Version: `v0.1`
>
> Date: `2026-09-11`
>
> Scope: `UniAgent realization seam / Codex simulation / DSH product`
>
> Governing Change: `UAR-001`
>
> Successor Authority: [UniAgent Realization Baseline v0.1](../architecture/uniagent-realization-baseline-v0.1.md)
>
> Forbidden Boundary: 本文不修改 Target Product Architecture，不授权实现，
> 不定义 Development Harness 流程，不把 Codex/DSH Host session、tool、event、
> transport 或 goal 语义提升为 Product authority。

---

## 0. 文档目的

本文回答一个有界问题：**如何让 Codex 与 DeepSeek Harness（DSH）分别成为
同一个 UniAgent 产品语义的完整实现，同时保持两者不同的使用定位与演进路径？**

目标形态是：

- `Codex-backed UniAgent`：完整的 **Simulation Realization**，用于产品语义
  测试、场景模拟、差分验证与回归；
- `DSH-backed UniAgent`：完整的 **Product Realization**，通过定制 Profile、
  lifecycle、state、capability 与 Host integration 逐步达到产品级；
- 两者都不是普通 AI Coding workflow，也不是 UniAgent 内部的 reasoning/model
  Adapter；
- 两者必须在同一组 Product invariants 与 conformance scenarios 下可替换比较。

本文只形成设计候选和 Review Gate；H1 的已接受决定由 ADR-0019 记录。本文不创建
其他 ADR、详细路线图或实现授权。

## 1. 上游 Authority 与继承关系

本文继承而不重定义以下产品事实：

1. [Target Product Architecture Baseline](../architecture/product-architecture-baseline-l0-l3.md)
   定义 UniAgent、Primary Goal、Execution Contract、Primary Run、Uni Kernel、
   Owner、Authority、Lifecycle 与 Replaceability Contract。
2. [Inter-Component Protocol Baseline](../architecture/protocols/inter-component-protocol-baseline-l1-l3.md)
   定义 P1 Contract Proposal、P18 Runtime Outcome、P19 Goal Evaluation；
   ADR-0019 已锁定 Primary Run 合法激活后的 Kernel driver，Run activation、
   Session correlation 与 Contract authoring questions 仍未裁决。
3. [CONTEXT.md](../../CONTEXT.md) 定义当前 canonical terms；H1 对应的 Kernel
   self-driven 术语已写回，其余 realization 词汇仍为 candidate。

若 Host 的能力、限制或默认行为与上述产品语义冲突，**适配 Host，而不是修改
产品 Authority 来迁就 Host**。

## 2. 当前产品基线

当前 cardinality 保持：

```text
1 Product Session
  └── 1 Primary Goal
        └── 1 Primary Run
```

稳定责任链保持：

```text
User
  → UniAgent owns Primary Goal
  → UniAgent authors Execution Contract Proposal
  → Run Model admits immutable Contract View
  → Uni Kernel owns the bounded Primary Run execution boundary
  → Uni Kernel emits immutable Runtime Outcome exactly once
  → UniAgent evaluates Goal satisfaction
  → User / Product Session receives Goal Evaluation
```

本设计不得让 Codex 或 DSH 改变以下事实：

- UniAgent 独占 Goal / Contract authoring / Goal Evaluation Authority；
- Runtime Outcome classification 不等于 Goal Satisfaction；
- UniAgent 不拥有 Current WorldBelief、Run State、Assurance Judgment、canonical
  binding 或 external effect delivery；
- 所有现实 Effect 仍必须经过 Uni Kernel 的 Effect Boundary；
- Host capability strength、模型能力或 deployment location 不产生 Authority；
- terminal Runtime Outcome 发出后不得恢复 Run 或继续产生现实 Effect。

## 3. 候选术语

### 3.1 UniAgent Realization

**Definition**：在一个具体 Host Runtime 上完整承担 UniAgent 产品职责、遵守
Product lifecycle 并满足 Conformance Surface 的实现。

**Required behavior**：

- 接收并保持一个 Product Session 内的用户意图；
- 创建并解释 Primary Goal；
- 形成全局策略，但不把计划变成 canonical execution truth；
- author Execution Contract Proposal；
- 消费 immutable Runtime Outcome；
- 形成 immutable Goal Evaluation；
- 对不可验证、Host failure 或缺失输入 fail closed。

**Not**：模型 Provider、prompt、单次 agent turn、普通 AI Coding workflow、
Uni Kernel driver、Capability Plane 或仅做协议转发的 Adapter。

### 3.2 Codex-backed UniAgent

以 Codex SDK 或 App Server 提供的 thread、turn、event、approval、tool 与 resume
机制承载 UniAgent lifecycle 的完整 Simulation Realization。

它是一个真实、非确定性的 realization，不是 deterministic fake。它的结果必须
由公共 Conformance Surface 约束，而不能因“仅用于测试”降低产品语义要求。

### 3.3 DSH-backed UniAgent

以 DSH Product Profile 组合自定义 Agent Loop、Product state、tool policy、
Kernel bridge、persistence 与 Host integration 的完整 Product Realization。

它不是一个“UniAgent 巨型插件”。Product Profile 是组装根；各插件分别实现
内部职责和 seam，不能复制第二套 Agent 或第二套 Product authority。

### 3.4 Host Runtime

承载 realization 的现成 Harness Runtime。当前候选为 Codex Runtime 与 DSH
Runtime。Host Runtime 提供 execution substrate，不自动拥有 Product semantics。

### 3.5 Host Session

Codex Thread/Session 或 DSH Session。它可以承载 history、events、resume、fork、
telemetry 与实现私有状态，但不是 Product Session 的同义词。

### 3.6 Conformance Surface

两个 realization 共同接受的产品语义测试面。它由 canonical inputs、records、
lifecycle transitions、fail-closed rules 与 observable evidence 组成，不包含 Host
专有 transcript、事件名、线程号、工具调用格式或 transport。

### 3.7 Adapter

只用于描述 realization 内部的 Host seam，例如：

- Codex App Server event → realization lifecycle event；
- DSH SessionEvent → realization private state projection；
- realization Contract Proposal → Kernel ingress；
- Kernel Runtime Outcome → Host input/event。

Adapter 不得拥有 Primary Goal、Execution Contract、Goal Evaluation 或完成判定。

## 4. 顶层逻辑形态

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
             Codex Runtime                     DSH Runtime
          SDK / App Server / MCP       Product Profile / Agent Loop /
             / approvals                 Session Events / policies
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

图中的横向可替换点位于 **UniAgent Conformance Surface**。Codex/DSH 专有
机制位于 realization 内部；它们不是共享产品 Interface 的一部分。

## 5. 外部 Interface 的深度要求

UniAgent external seam 必须保持小而深。调用者只应理解：

- Product Session correlation；
- 用户意图输入；
- Product lifecycle state；
- Goal/Contract/Evaluation 的 canonical identity 与 version；
- typed failure / unavailable / escalation；
- 可观察的 Product records 与 evidence references。

调用者不应理解：

- Codex thread/turn/item 或 DSH session/turn/step event vocabulary；
- prompt 拼装、模型消息格式、token budget 或 context compaction；
- MCP、JSON-RPC、Cordis、Plugin、Profile 或 transport 细节；
- Host 内部的 retry、fork、approval request 或 transcript layout。

删除这一 seam 后，Host 差异会泄漏到 Product Host、Kernel bridge、测试与
持久化多个调用点；因此这个 seam 具有真实的 locality 与 leverage。两个
realization 已经构成建立该 seam 的真实 buyer。

本文不锁定具体方法、DTO 或 namespace；Interface 在这里包含输入输出、
invariants、ordering、error modes 与 lifecycle，而不仅是类型签名。

## 6. Product State 与 Host State

### 6.1 身份关系

| Product concept | Codex carrier | DSH carrier | 约束 |
|---|---|---|---|
| Product Session | Codex session/thread mapping record | DSH session mapping record | Product SessionId 独立生成/恢复，不从 Host id 隐式推导 |
| Primary Goal | realization-owned typed record | realization-owned typed record/event | Codex/DSH 自带 goal 只能作为私有机制或 projection |
| Execution Contract | realization-owned versioned proposal | realization-owned versioned proposal/event | Host message 不是 Contract；只有 P1 admission 建立 Contract View |
| Primary Run | Kernel RunId | Kernel RunId | Host turn/step/round 不得冒充 Run 或 cycle |
| Runtime Outcome | Kernel immutable envelope | Kernel immutable envelope | Host 不重判、不改写、不补齐 |
| Goal Evaluation | realization-owned immutable record | realization-owned immutable record/event | transcript/final answer 不是 canonical evaluation |

映射必须显式、可恢复、可诊断。禁止：

- `ProductSessionId = HostSessionId` 的无声明别名；
- 根据 transcript 反向考古 canonical state；
- Host resume 成功即宣称 Product Session 恢复成功；
- Host fork 自动产生新 Product Goal 或新 Primary Run；
- Codex/DSH goal 状态覆盖 Primary Goal revision 或 satisfaction。

### 6.2 语义 Owner 与物理存储分离

Product semantic Owner 决定谁有权创建、修改和终止记录；物理存储位置只回答
字节放在哪里。DSH Session Event Log 可以物理保存 Product records，Codex
rollout 或外部 simulation store 也可以保存 mapping，但物理共置不产生新
Authority。

每一类 canonical Product record 必须只有一条合法写入路径；Host event 或
transcript 只能作为：

- realization-private execution evidence；或
- 由 canonical Owner 产生的 typed Product record 的持久化载体。

## 7. Codex-backed UniAgent — Simulation Realization

### 7.1 定位

Codex realization 用于：

- 产品场景模拟；
- Contract authoring / Goal Evaluation 行为探索；
- 双 realization differential conformance；
- recovery、failure、approval 与 missing-tool 场景验证；
- DSH Product Realization 的回归对照。

它不用于：

- 宣称 production availability、SLA 或 multi-tenant readiness；
- 充当 deterministic fake；
- 让 Codex coding task lifecycle 取代 Product lifecycle；
- 让文件、shell、Git 或代码修改成为默认产品能力。

### 7.2 候选内部形态

```text
Codex-backed UniAgent
├── Product Session Mapper
├── Product Instruction / Context Builder
├── Structured Product Record Decoder
├── Kernel Simulation Bridge or bounded Kernel Bridge
├── Approval / Capability Policy
├── Host Event Recorder
└── Conformance Projection
     └── Codex SDK or App Server
```

优先使用已发布稳定 SDK 承担 start/continue/resume；只有需要更细粒度 event、
approval 或 custom-client control 时才使用 App Server，并固定 Codex runtime 与
generated schema 版本。WebSocket 与 experimental dynamic tools 不进入必须通过的
Conformance Surface。

### 7.3 Fail-closed requirements

- required Kernel bridge 缺失或初始化失败 → 不创建/继续 Product Run；
- structured Product record 无法解析或不满足 schema → 不从自然语言猜测；
- Host thread 恢复但 Product mapping 缺失 → `PRODUCT_SESSION_UNRECOVERABLE`；
- 模型结束但无合法 Contract Proposal / Goal Evaluation → 显式 incomplete；
- approval 被拒绝 → 不通过备用 shell/file 路径绕过；
- Host fork → 默认只产生 simulation branch，不改变 canonical Product Session。

### 7.4 当前证据与限制

截至 2026-09-11，本机 `codex-cli 0.149.1`。OpenAI 一方资料表明：

- [Codex SDK](https://learn.chatgpt.com/docs/codex-sdk) 可在应用中启动、继续和
  resume local Codex threads；Python SDK 通过 JSON-RPC 控制本地 App Server；
- [Codex App Server](https://learn.chatgpt.com/docs/app-server) 提供 thread
  start/resume/fork、事件流、approvals、goal state 与 schema generation；
- [Open Source](https://learn.chatgpt.com/docs/open-source) 列出 CLI、SDK 与
  App Server 源码，同时明确 IDE extension 与 Codex cloud 不开源；
- [Feature Maturity](https://learn.chatgpt.com/docs/feature-maturity) 要求实验性
  能力按不稳定能力处理。

这些证据证明可集成性，不证明 Codex 已满足非 Coding Product semantics。该问题
必须由 Tracer Bullet 和 conformance evidence 回答。

## 8. DSH-backed UniAgent — Product Realization

### 8.1 定位

DSH realization 是目标产品实现。它必须通过专用 Product Profile 组装，而不是
在通用 Coding Profile 上叠加一个 prompt 或巨型插件。

### 8.2 候选内部形态

```text
DSH UniAgent Product Profile
├── UniAgent lifecycle / Agent Loop realization
├── Product Session and typed Product-state module
├── Goal / Contract authoring module
├── Goal Evaluation module
├── trusted Kernel Bridge
├── product-only capability and tool policy
├── model-routing policy
├── approval / isolation / redaction policy
├── persistence / recovery / audit projection
├── Product Host / UI integration
└── DSH session / telemetry providers
```

模块可以共享一个部署单元，但不得合并 Product Owner 或复制 canonical state。

### 8.3 DSH seam 使用原则

- Session Event：只保存需要 resume/replay 的持久事实；新增模型可见输入必须可从
  session log 重建；
- Agent Event：拦截 active agent lifecycle，不作为 durable Product truth；
- Capability Event/Service：承载模型、tool policy、Kernel bridge、storage 与
  telemetry 等可替换能力；
- Agent preset / isolate realm：为 Product Session 建立允许能力集合；
- Product Profile：移除默认 Coding Tools，仅保留显式 allowlist；
- Kernel Bridge：向 Agent 暴露产品级 typed operation，不暴露直接 device effect、
  shell、filesystem 或底层 L2 Authority；
- Product record：由 canonical Owner 产生后再写入 Session Event，不允许监听器
  从 transcript 二次推导并写回。

### 8.4 Fail-closed requirements

- Product Profile 中出现未批准 Coding Tool → profile validation failure；
- Kernel Bridge、persistence 或 required policy provider 缺失 → session 不启动；
- Session log 可恢复但 Product record projection 不完整/冲突 → 不继续 Run；
- cancellation、model failure、tool timeout 必须形成 typed lifecycle result；
- plugin unload/reload 不得撤销已经形成的 canonical Product facts；
- telemetry 丢失不得改变 Product truth，也不得被当作 audit completeness；
- 任何模型/插件不得绕过 Kernel Effect Boundary 直接触达现实环境。

### 8.5 当前证据与限制

本次 evidence snapshot 使用 DSH upstream commit
`b150a551b8d465e31e418e1b2eaf5e79bbb7d28e`。本机源码存在未提交修改，因此只把
该提交的一方文档作为架构证据：

- [DSH Architecture](https://github.com/deepseek-ai/deepseek-harness/blob/b150a551b8d465e31e418e1b2eaf5e79bbb7d28e/docs/architecture.zh.md)
  明确 model adapter、tool registry、session log、agent loop 均为可替换插件，
  并说明 Profile/Bundle、Session/Agent/Capability events 与 capability seam；
- [DSH README](https://github.com/deepseek-ai/deepseek-harness/blob/b150a551b8d465e31e418e1b2eaf5e79bbb7d28e/README.zh.md)
  明确当前仍处 Developer Preview，未来可能出现 breaking changes；
- [Persistence](https://github.com/deepseek-ai/deepseek-harness/blob/b150a551b8d465e31e418e1b2eaf5e79bbb7d28e/docs/subsystems/persistence.zh.md)
  提供 session event persistence、resume/fork 等实现依据。

这些证据证明 Product Profile 的构造自由度，不证明 production compatibility、
tenant isolation、RBAC、disaster recovery、audit completeness 或 SLA。

## 9. 双 realization 一致性与允许差异

### 9.1 必须一致

| Axis | Required conformance |
|---|---|
| Product cardinality | 1 Session / 1 Primary Goal / 1 Primary Run |
| Owner/Authority | 与 Product baseline 完全一致 |
| Contract | author/admission/version/failure 语义一致 |
| Runtime Outcome | immutable、exactly once、不可重判 |
| Goal Evaluation | immutable、幂等、不回写 Runtime truth |
| Terminal | terminal 后无新现实 Effect |
| Failure | 缺输入、缺 provider、不可解析、冲突时 fail closed |
| Correlation | Product ids 与 Host ids 显式映射，可恢复、无静默别名 |
| Evidence | Product records 和 conformance evidence 可引用、可重放 |

### 9.2 允许不同

- 模型、prompt、context assembly；
- 内部 turn/step 数量；
- Host event vocabulary 与 transport；
- approval interaction；
- persistence provider 与物理布局；
- latency、cost、token usage；
- 自然语言 rationale 的非语义措辞；
- simulation-only trace 与 DSH production telemetry。

### 9.3 禁止比较方式

- transcript 逐字相等；
- tool-call sequence 逐步相等；
- Host thread/session id 相等；
- token 或 step 数量相等；
- 仅凭最终自然语言答案判断 conformance。

## 10. Product Context 与 Development Harness Context

同一个 Codex/DSH 可执行文件可能在两个 Context 中出现，但二者必须视为不同的
系统使用：

| Context | 目的 | Canonical state | Skills/Tools | Completion |
|---|---|---|---|---|
| Development Harness | 开发、Review、Verify UniClaw | Change State、Plan、WorkItem、代码与开发 Evidence | UniFlow、coding tools、repo skills | UniFlow verification |
| Product Runtime | 运行 UniAgent，为用户完成现实目标 | Product Session、Goal、Contract、Run、Outcome、Evaluation | product allowlist、Kernel ingress | Goal Evaluation |

强制隔离：

- Development WorkItem 不得成为 Primary Goal；
- UniFlow lifecycle 不得成为 Product lifecycle；
- AGENTS.md / coding Skills 不得作为 Product instruction authority；
- 开发 Session 不得 resume 为 Product Session，反之亦然；
- Git、shell、file edit、code review 等开发能力默认不进入 Product Profile；
- Product Goal Evaluation 不得被开发 Harness 的“任务完成”结论替代。

## 11. Kernel 驱动模式：已接受 Human Decision

ADR-0019 已闭合 Deferred ①：Primary Run 合法激活后由 Uni Kernel self-drive。
这不把 Contract admission 等同于 activation；accepted Contract View 只是合法
激活的必要前置条件，activation seam 留给 R1 闭合。

### Alternative A — Kernel self-driven（推荐）

```text
UniAgent --P1 Contract Proposal--> Run Model admission
accepted Contract View --required precondition--> legal Primary Run activation
legal Primary Run activation --DEFERRED ⑰ / NOT_DESIGNED--> activated Run
activated Run --Uni Kernel self-drives--> terminal Runtime Outcome
Uni Kernel --P18 Runtime Outcome--> UniAgent
```

UniAgent 只承担 Product supervision，不逐步调用 SelectIntent/Act/
EvaluateTerminal。必要的 cancel/pause/escalation 是独立 lifecycle command，
不能成为每 cycle orchestration。

**优点**：

- UniAgent external Interface 最小、最深；
- Codex/DSH 不需要理解 Kernel L2 操作序列；
- Host crash/retry 不会直接重放现实 Effect；
- Kernel 保持完整 execution boundary 与本地 recovery authority；
- 两个 realization 更容易做规范化 conformance。

**代价**：Kernel 必须拥有明确的 autonomous run driver 与 progress/event surface；
UniAgent 只能通过有界 supervision command 影响运行。

### Alternative B — UniAgent step-drives Kernel

UniAgent realization 逐 cycle 调用 Process/SelectIntent/Act/EvaluateTerminal。

**优点**：Host agent loop 可以直接承载全局编排，短期原型直观。

**风险**：Kernel L2 sequencing 泄漏到 Codex/DSH；Host retry、resume 或模型输出
可能影响 Effect delivery；两个 realization 难以保持同构；UniAgent 易获得
Control/Assurance/Run Authority。

### Alternative C — 独立 Product Session Coordinator

由第三个 Product module 同时驱动 UniAgent 与 Kernel。

**优点**：Session lifecycle 可独立管理。

**风险**：新增第三个 orchestration owner，极易形成 God Context；在单 Goal、
单 Run baseline 下尚无足够 buyer 证明其复杂度。

### Accepted decision

Human 于 2026-09-11 接受 **Alternative A — Kernel self-driven**，并由
[ADR-0019](../adr/0019-primary-run-is-self-driven-by-uni-kernel.md) 固化：accepted
Contract View 只是合法激活的必要前置条件；Primary Run 合法激活后，由 Uni
Kernel internal run driver self-drive；UniAgent 与 Codex/DSH Host 不逐 cycle
驱动 Kernel。

该决定只闭合 driver Authority 与 seam shape。具体 lifecycle Interface、
Tracer Bullet 与路线图仍保持未授权；SOL architecture re-review 尚未完成。

## 12. Conformance Model

### 12.1 Canonical fixture

首个 fixture 只覆盖一条完整监督弧：

```text
User Intent
→ Primary Goal
→ Execution Contract Proposal
→ Contract Admission
→ legal Primary Run activation（R1 必须先闭合的独立 seam）
→ deterministic Kernel Simulation
→ Runtime Outcome
→ Goal Evaluation
```

Kernel Simulation 必须是 deterministic fake/replay；Codex-backed UniAgent 本身
不是 fake。这样才能把 UniAgent non-determinism 与 Kernel behavior 分开。

### 12.2 Conformance layers

1. **Contract**：schema、identity、version、Owner/Authority 与 forbidden path；
2. **Deterministic**：canonical record derivation、idempotency、mapping projection；
3. **Scenario**：成功、部分满足、不满足、不可判断、拒绝、取消、恢复；
4. **Environment**：真实 Codex/DSH Host start/resume/failure/approval evidence。

### 12.3 必测场景

| # | Scenario | Expected Product behavior |
|---|---|---|
| C1 | 相同用户意图，两 Host 正常执行 | 形成语义等价的 Goal/Contract/Evaluation records |
| C2 | Contract 非法或不完整 | admission fail closed；无 Primary Run |
| C3 | Host resume 成功、Product mapping 缺失 | Product Session 不恢复 |
| C4 | Host 重复投递 Runtime Outcome | 同输入幂等，不产生第二 canonical Evaluation |
| C5 | Kernel 已 terminal，Host 再次调用工具 | 无新 Effect，无第二 Runtime Outcome |
| C6 | 模型只输出自然语言结果 | 不猜测 canonical record，标记 incomplete |
| C7 | required bridge/provider 缺失 | session/run 不启动或安全停止 |
| C8 | approval 拒绝/cancel | 无绕过路径，产生 typed lifecycle result |
| C9 | Host fork | 不静默创建第二 Product Goal/Run |
| C10 | Codex 与 DSH rationale 措辞不同 | 只要 canonical semantics 等价即可通过 |
| C11 | accepted 后重复提交同 version P1 | 返回同一 Contract View；零 activation、零 Run progression、零 Effect 副作用 |

## 13. Product-readiness Gate

### Codex Simulation Gate

满足以下条件才可声明“合格模拟实现”：

- 通过 C1–C11；
- Product/coding context 可验证隔离；
- SDK/App Server runtime 与 schema 已固定；
- required capability 缺失时 fail closed；
- deterministic fake/replay 仍作为底层合同测试存在。

### DSH Product Gate

除 C1–C11 外，还必须证明：

- Product Profile 不包含未批准 Coding Tools；
- crash/resume/cancel/model failure/tool timeout 无 authority 穿透；
- Product state 可恢复、可迁移、可审计；
- tenant/identity/credential/secret isolation；
- network/process/filesystem Effect boundary；
- version upgrade、rollback、backup/restore；
- telemetry 缺失不影响 Product truth；
- 真实 Host receipt 证明 provider、profile、policy 与 Kernel bridge 生效。

在这些证据出现前，`DSH-backed UniAgent` 只能称为 Product Realization Candidate，
不能称为 production-ready。

## 14. 阶段级 Roadmap Skeleton（非实施计划）

| Stage | Decision/Evidence objective | Exit gate |
|---|---|---|
| R0 Architecture Review | 复审已接受的 Kernel driver boundary 与 shared conformance level | SOL review outcome + Human H1/ADR-0019 |
| R1 Contract Closure | 闭合 Session correlation、Goal→Contract authoring 与 legal Run activation semantics | P1/P18/P19 + activation/Session lifecycle 可表达 |
| R2 Dual Tracer Bullet | Codex/DSH 跑同一 deterministic Kernel fixture | C1–C11 evidence |
| R3 Codex Simulation | 固化 simulation profile、schema、replay 与 failure corpus | Codex Simulation Gate |
| R4 DSH Product Foundation | Product Profile、state、Kernel bridge、policy、recovery | bounded product scenarios pass |
| R5 Product Hardening | security、operations、upgrade、audit、isolation | DSH Product Gate |
| R6 Adoption | DSH 成为选定 Product Realization；Codex 保留模拟职责 | Human adoption decision |

每一阶段未来必须另立 Change，并按
`行为 → 模型 → 运行时 → 测试 → 证据` 切成垂直 tracer bullet。本文不决定工期、
依赖排序、文件清单或实施责任人。

## 15. Decision Ledger

### 已继承，不重开

- UniAgent 与 Uni Kernel 是 L1 peers；
- UniAgent 独占 Goal/Contract authoring/Goal Evaluation Authority；
- Uni Kernel 与六个 L2 Owners 保持执行/现实 Authority；
- 1 Product Session / 1 Primary Goal / 1 Primary Run；
- Host capability 不改变 Product Authority；
- Effect、Outcome Proof、Runtime Outcome 与 Goal Evaluation 分离。

### 已接受 Human Decision

- Primary Run 合法激活后由 Uni Kernel internal run driver self-drive；
  UniAgent/Host 不逐 cycle 驱动。P1 admission 不负责 activation（ADR-0019）。

### 本文候选

- Codex/DSH 是两个完整 UniAgent Realization；
- Codex = Simulation Realization；DSH = Product Realization；
- Host Session 与 Product Session 显式映射且非同义；
- 共同 seam 是 Product-semantic Conformance Surface；

### Deferred

- accepted Contract View → legal Primary Run activation protocol；
- Product Session identity algorithm 与物理 persistence owner；
- Goal revision、multi-run、continuation、跨 Session identity；
- lifecycle command vocabulary（cancel/pause/escalate）；
- progress/event surface；
- C#/Host transport 与 serialization；
- DSH plugin/package 切分；
- Codex SDK 与 App Server 的最终组合；
- deployment topology、tenant model、SLA 与运维设计。

## 16. Human Decision Record 与 SOL Review Entry

### Human Decision H1 — ACCEPTED

> **accepted Execution Contract View 是 Primary Run 合法激活的必要前置条件；
> Primary Run 合法激活后，由 Uni Kernel self-drive 至 immutable Runtime
> Outcome。UniAgent 不逐 cycle 驱动 Kernel，只保留 P1/P18 监督弧及未来独立
> 定义的有界 lifecycle commands。**

记录：2026-09-11，Human accepted；见 ADR-0019。

### SOL Review entry

SOL Review 需要验证：

- ADR-0019 是否保持 UniAgent、Kernel 与六个 L2 Authority 不变；
- P1 admission、Deferred ⑰ activation 与 post-activation driver/P18 是否严格
  分离并形成足够小而深的 seam；
- Host crash/resume/retry 是否完全不能产生第二条 Effect path；
- Deferred ⑯ 是否足以阻止 lifecycle command 膨胀为 step driver；
- 双 realization Conformance Surface 是否仍然 Host-neutral。

SOL Review 通过后才可由 Human 授权进入 R1，闭合 Session correlation、
Goal→Contract authoring 与 legal Primary Run activation；R1 通过后才规划 R2
双 Tracer Bullet。

### SOL Review result — CHANGES_REQUIRED（2026-09-11）

1. P1 将 admission 与 start/resume activation 混合，可能让 Host retry 成为第二条
   lifecycle/driver 入口；必须拆开并补齐独立 Producer/Consumer/failure 语义。
2. P2 的 post-action observation sequence coordination 修改超出 UAR-001 的 H1
   窄收口，应撤回或通过独立决策证明 buyer 与 Authority。
3. `CONTEXT.md` 新词条超过 glossary 的紧凑定义职责，应压缩为一至两句 WHAT，
   把操作顺序与未来 command 约束留在 ADR/Protocol。
4. 本文开头仍有“不创建 ADR / Kernel driver 尚未裁决 / 新词不得写回”的陈旧
   表述，需要与 H1 accepted 和 ADR-0019 对齐。

修正前不得把本候选升级为 architecture authority，也不得进入 R1。

### Review repair — APPLIED（2026-09-12）

1. P1 已恢复为纯 Contract Proposal/admission；重复 admission 只返回同一
   Contract View，不得激活、恢复、推进 Run 或产生 Effect。
2. accepted Contract View → legal Primary Run activation 已明确为独立
   Deferred ⑰；唯一 Producer/Consumer、identity/correlation、重复/并发、失败与
   恢复语义留给 R1，不在 UAR-001 内臆造。
3. P2 的 post-action observation sequence coordination 归属扩张已撤回。
4. `CONTEXT.md` 词条已压缩为 WHAT-only 定义；候选开头和 Decision Record 的
   H1 前陈旧状态已同步。

### SOL re-review result — PASS（2026-09-12）

- Standards：`PASS`；High/Medium/Low findings = `0/0/0`。
- Spec：`PASS`；High/Medium/Low findings = `0/0/0`，Acceptance 1–10 全部通过。
- 首轮 P1 seam、CONTEXT glossary、陈旧状态、Host retry、P2 scope 与同步声明
  findings 全部 `CLOSED`。

复审通过不产生 architecture authority 或实现授权。本候选保持 `Authority:
NONE`；进入 R1 仍需要独立 Human authorization。

### Human closeout — ACCEPTED（2026-09-12）

Human 接受当前小步结果并关闭 UAR-001，明确暂不进入 R1。该 closeout 只关闭
R0 设计/Review Change，不把本候选升级为 architecture authority，也不授权
R1、详细路线图或实现。

## 17. UAR-001 Gate Recommendation（历史）

```text
Feasibility: PASS
Human Decision H1: ACCEPTED (ADR-0019)
Architecture Candidate: SOL_REVIEW_PASS / R0_CLOSED
Architecture Authority: NONE (at UAR-001 closeout)
Implementation: NOT_AUTHORIZED
Detailed Roadmap: NOT_AUTHORIZED
Next Stage: R1 Contract Closure NOT_AUTHORIZED
```

## 18. UAR-002 Adoption Note

UAR-002 只将本候选中的稳定决策提炼并冻结到
[UniAgent Realization Baseline v0.1](../architecture/uniagent-realization-baseline-v0.1.md)，
由 ADR-0022 记录双 realization 取舍。本文件保留为 `Authority: NONE` 的设计
历史；其中 Host 技术选型、内部候选形态、阶段 skeleton 与 deferred 细节不具有
规范效力。R1 仍未授权。
