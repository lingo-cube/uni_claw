# UniClaw 架构与协议整体审阅（2026-10-11）

> Status: `COMPLETE / DELIVERED-BY-ARCH-ALIGN-001`
> Authority: `NONE`
> Scope: 当前 `uni-harness` 工作树中的 Product Architecture、Kernel、Agent、Capability、Core、Host/Realization、Workspace、Harness 与协议资料。
> 说明：本文是审阅底稿，不取代产品架构权威；已接受方向由 `changes/ARCH-ALIGN-001` 与 ADR-0042–0047 固化。协议主链与外围审阅矩阵（§13/§15）已随后续轮次补齐并随该 Change 一并交付（2026-10-12 收口）；开放项以 §13.3/§15.1 各行状态为准，处置归属见 §14.2 与 §15 末尾划分。

## 1. 审阅目标

本次目标不是只修正 L0/L1 名称，而是回答四个问题：

1. UniClaw 由哪些产品组件组成？
2. 哪些是跨组件的产品语义对象，哪些是组件内部责任？
3. 每个状态、判断和输出由谁拥有，生命周期如何结束？
4. 组件之间的协议是否表达了正确的 Owner、输入、输出、失败、重复、迟到和证据语义？

完成后，任何概念都应该能回答：它是什么、属于哪个语义面、谁是 canonical owner、谁可以读写、何时失效、是否已实现、依据是哪份文档或代码。

## 2. 当前权威与现实状态

### 2.1 主要来源

| 来源 | 当前内容 | 当前判断 |
|---|---|---|
| `AGENTS.md` | GREENFIELD 产品与 Harness 的总边界 | 当前工作约束入口 |
| `docs/architecture/product-architecture-baseline-l0-l3.md` | Product T0–T3、Semantic Lifecycle、Owner、Authority、Lifecycle、不变量 | `ADOPTED / PRODUCT-T0-T3-CLOSED`；稳定路径保留，旧 L0–L3 仅作历史映射 |
| `docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md` | P1-P26 组件间协议 | 协议 canonical 落点；包含 verified、known gap、placeholder、deferred |
| `docs/architecture/uworld-protocol-baseline-l4.md` | UIWorld 作为 World Model 的 UI L4 realization | FROZEN v0.3 |
| `docs/architecture/uniagent-realization-baseline-v0.1.md` | UniAgent 双 realization 与 Conformance Surface | FROZEN product-realization baseline |
| `docs/architecture/perception-provider-baseline-v0.1.md` | 感知 Provider 组件与 transport | FROZEN component baseline；内部另用 L0/L1/L2 |
| `docs/architecture/repository-role-boundaries-v0.1.md` | 根目录与仓库职责 | ACCEPTED component baseline |
| `CONTEXT.md` | 术语表与 Owner/Authority 词汇 | 当前工作树有未提交修改，不能自动视为已接受架构 |
| `docs/adr/` | 不可逆取舍 | 解释 WHY，不能替代整体 Owner 矩阵 |
| `src/`、`web/`、`dsh/`、`product/` | 当前实现与装配 | 只能证明实现事实，不能单独证明架构已接受 |

### 2.2 已确认的产品主线

```text
User intent
  → UniAgent interprets Goal and authors Contract
  → Run Model admits Contract View
  → legal activation
  → Uni Kernel self-driven run
      → observation → Evidence admission → World reconciliation
      → Control Intent → Grounding/Canonical Binding → Assurance
      → Effect Gate → Dispatch → Receipt
      → post-action observation → reconciliation → Effect Verification
      → repeat or terminal proof
  → Run Model records Outcome State
  → Uni Kernel emits Runtime Outcome
  → UniAgent evaluates Goal satisfaction
```

这条主线的责任分工已经相对稳定；当前最大问题是描述它的层号、语义对象和组件名称混在一起。

## 3. 第一轮审阅结论

### 3.1 已解决：Product T0–T3 与语义生命周期分轴

原产品基线 §3 的 L0 同时放入：

- 组件：`UniAgent`、`Uni Kernel`
- 生命周期对象：`Primary Goal`、`Execution Contract`、`Primary Run`
- 事实或判断：`Evidence`、`WorldBelief`
- 终局产物：`Runtime Outcome`、`Goal Evaluation`

基线 §5 又把 `UniAgent`、`Uni Kernel`、`Capability Plane`、`Memory System` 定义为 L1。Owner 已裁决：Product T0–T3 只表达拓扑，四个顶层组件位于 Product T1；Goal、Contract、Run、Evidence、WorldBelief、Outcome、Evaluation 进入独立 Product Semantic Lifecycle 与 Evidence/Belief flow。该方向已由 ADR-0042 固化。

### 3.2 仓库存在多套未命名空间化的层级轴

当前至少存在以下层级体系：

| 层级轴 | 示例 | 实际含义 |
|---|---|---|
| Product Architecture | L0-L3 | 产品上下文、组件、Kernel 责任域、内部模型 |
| Capability | L0 `ICapability` → L1 协议 → L2 组合实现 | 能力继承与协议接入层级 |
| Perception Provider | L0 Plane、L1 数据流、L2 transport/contract | Provider 组件内部结构 |
| Decision Granularity | L0 精细指令、L1 路径计划、L2 泛化策略 | Agent 决策粒度，不是系统架构层 |
| Profile Assembly | L1 产品定义、L2 产品加载、L3 DSH adapter、L4 deployment | Profile 装配链 |
| Harness Evidence | L0-L5 或 L1-L4 | 研发证据与上下文加载分级 |

这些层级描述的是不同对象，不能裸写 `L0`、`L1` 互相引用。

### 3.3 已解决：基线元数据与消费规则统一

原产品架构基线头部写 `CANDIDATE_FOR_ADOPTION / Authority: NONE`，但：

- `AGENTS.md` 将它列为 GREENFIELD 产品代码的直接权威；
- `E2B-001` 把 Target v0.1 记为全量采纳、L0-L3 CLOSED；
- 多个 README 和协议文档按 CLOSED 基线消费它。

Owner 已裁决并已落文档：基线状态为 `ADOPTED / PRODUCT-T0-T3-CLOSED`，Authority 为 `PRODUCT_ARCHITECTURE_BASELINE`；旧 Product L0–L3 仅保留历史引用和迁移映射。

### 3.4 目标架构和实现状态没有统一标记

当前同时存在：

- 已冻结的产品、协议、UIWorld、UniAgent realization、Perception Provider 和 Simulation component baselines；
- `Memory System` 的候选占位；
- Core 的候选共享记录模型；
- Workspace 和 Profile 设计稿仍是 `Authority: NONE`，但对应实现已经部分存在；
- 产品 Profile、Skill、Task Profile 的实现提交与旧设计文档状态之间存在时间差。

代码存在、测试通过、组件基线冻结、产品语义接受、协议已验证是五种不同状态，不能用一个 `implemented` 概括。

## 4. 概念分组（暂定，不是最终裁决）

### 4.1 顶层产品组件

```text
UniAgent
Uni Kernel
Capability Plane
Memory System
```

其中：

- `UniAgent` 是目标、全局策略、契约和目标评价的认知 Owner；
- `Uni Kernel` 是 UI 领域运行时和 Runtime Outcome emission boundary；
- `Capability Plane` 提供可替换能力，不获得 Product canonical authority；
- `Memory System` 管理跨时间记录和召回，目前仍无真实 buyer 的产品实现。

### 4.2 产品语义对象

```text
Product Session
Primary Goal
Execution Contract
Execution Contract View
Primary Run / Run State
Evidence Record
WorldBelief Revision
Control Intent
Canonical Binding
Effect / Attempt
Outcome Proof / Outcome State
Runtime Outcome
Goal Evaluation
```

这些对象描述产品运行中的事实、意图、判断和结果，不应与 `UniAgent`、`Uni Kernel` 这样的组件并列写成同类“架构节点”。

### 4.3 Kernel 内部责任域

```text
Evidence Ledger
World Model
Run Model
Control Loop
Assurance
Effect Boundary
```

它们分别拥有 Evidence、WorldBelief、Run State、Control Intent、Assurance Judgment 和 Canonical Binding/Effect Delivery，Uni Kernel 只保留组合边界和 Outcome emission authority。

### 4.4 正交实现面

| 面 | 定位 |
|---|---|
| Core | 跨领域 typed immutable semantic records/projection；当前候选，不是 Runtime Owner |
| Host / DSH | Composition Root、transport、provider/model binding 和 realization adapter |
| Workspace | 只读查询、关联和展示投影 |
| Provider | Capability 的外部 realization，不拥有 Product state |
| Development Harness | UniFlow、Change State、WorkItem、Harness Tool 和研发 Evidence |
| Product Skill / Profile / Task Profile | 产品装配与认知约束工件，不是新的 Runtime Owner |

## 5. 三套架构重排方案（已完成选择）

### 方案 A：系统上下文优先

```text
L0  UniClaw Product + User / Host / Device Environment
L1  UniAgent / Uni Kernel / Capability Plane / Memory System
L2  Kernel 六个责任域
L3  各责任域内部模型、协议和实现
```

优点：符合系统上下文 → 产品组件 → 子系统 → 内部实现的通常读法；`UniAgent` 和 `Uni Kernel` 成为同一层的产品组件。

代价：需要把当前 L0 的 Goal、Contract、Evidence、Outcome 移出层级，另列“Product Semantic Model”。

### 方案 B：产品内部拓扑优先

```text
L0  UniAgent / Uni Kernel / Capability Plane / Memory System
L1  产品语义生命周期与跨组件对象
L2  Kernel 六个责任域
L3  内部模型与协议
```

优点：直接符合“L0 是产品内部最高级组件”的直觉；对当前基线改动较小。

代价：L1 又会混合 Goal、Contract、Run、Evidence 等不同语义类型；如果继续把协议也称 L1-L3，仍可能产生二次混淆。

### 方案 C：架构拓扑与语义模型双轴（Owner 已选择）

不再让一套 `L0-L3` 同时表达组件、业务对象、决策粒度和证据级别：

```text
Product Topology
  T0  system context
  T1  UniAgent / Uni Kernel / Capability Plane / Memory System
  T2  Kernel responsibility domains
  T3  realization modules and internal records

Product Semantic Lifecycle
  Session → Goal → Contract → Run → Outcome → Goal Evaluation

Evidence / Belief Flow
  Observation → Evidence Record → WorldBelief → Intent → Effect → Verification

Protocol Graph
  P1-P26 + UIWorld L4 edges + Host/Workspace/Capability contracts
```

所有局部层级改用命名空间：`Product T1`、`Capability C1`、`Decision G1`、`Profile A2`、`Harness E3`。这样不会再把“决策 L0”误读成“产品架构 L0”。

代价：需要更新文档、README、注释和协议文件名中的层级引用；这是一次语义整理，但不要求立即迁移代码。

## 6. 已接受的架构方向

Owner 已接受方案 C 的“正交双轴”原则，并在产品拓扑轴上采用方案 A 的语义：

```text
Product T0：系统边界与外部参与者
Product T1：UniAgent、Uni Kernel、Capability Plane、Memory System
Product T2：Kernel 六责任域
Product T3：内部模型、协议实现和 Host/Provider realization
```

`Goal`、`Contract`、`Run`、`Evidence`、`WorldBelief`、`Outcome`、`Evaluation` 进入 Product Semantic Lifecycle，不再作为 T0/T1 组件。

## 7. 架构审阅后的协议审阅流程

架构方案接受后，再逐边审阅：

1. Product Semantic Lifecycle：Session/Goal/Contract/Run/Outcome/Evaluation 的关联、修订和终止。
2. P1-P26：逐边记录 Producer、Consumer、Meaning、Minimal Payload、Identity、Revision、Scope、Authority、Validity、Failure、Absence、Duplicate、Late、Retry、Evidence、Lifecycle 和当前实现状态。
3. O1-O12：紧接审阅 Task、Session、Host、Workspace、Capability、Profile、Launch、Harness 外围边，沿用同一套 Owner、输入输出、失败、重复、恢复和实现状态字段。
4. UIWorld L4：只审阅它作为 World Model realization 的边界，不把 UI 内部术语升格为 Product Core。
5. Capability/Provider：区分 Capability Definition、Implementation、Binding、Instance、Provider 和 Tool/Skill exposure。
6. Task/Profile/Launch：区分 Task、Task Profile、Task Instance、Product Session、Primary Run、Host Session 和 Workspace projection。
7. Workspace/Harness：确认查询、Trace、Evidence、Change State 和 Product Runtime 之间没有 authority 倒灌。

协议状态统一使用：`verified`、`known gap`、`known deviation`、`placeholder`、`deferred`、`not applicable`，禁止用“存在代码”代替“协议已验证”。

## 8. 第二轮裁决结果

1. `Core` 保持 Product T3 的跨域 immutable record/projection 正交面，不进入 Product T1，
   不成为 Product T2 Owner，也不形成第二 Runtime。
2. `Task Instance`、`Product Session`、`Primary Run`、`Host Session` 四个概念保持分离；
   当前范围可以一对一，但必须通过显式 Binding 关联，禁止从目录、transcript 或 Host
   session 推导 identity。
3. `Capability Definition / Implementation / Binding / Instance` 分别归 Product T1
   Capability Plane 管理面、Provider/Host realization、Composition Root/Task scope 和
   当前 Host/Task scope；不新增 Product T2。
4. 协议审阅采用同一 Change 的两段式：先审 Product Runtime 主链（Semantic Lifecycle、
   P1–P26、UIWorld），再审 Host/Workspace/Task/Capability/Provider/Profile/Launch/Harness
   外围边；两段共用同一 Owner/Authority 矩阵。

这些裁决已由 ADR-0043 和 `CONTEXT.md` 固化。

## 9. 第三轮裁决结果

1. UniClaw Runtime 根据 Task 创建 `taskInstanceId` 并分配 `productSessionId`；
   Task Launch 只提交/读取 binding；Host 只铸造 `hostSessionRef`；UniAgent 只消费
   和监督；Kernel 在 Contract View 接受并合法激活后才创建/驱动 `runId`。
2. 跨产品/Harness 边界只使用 `productSessionId`、`taskInstanceId`、`runId`、
   `hostSessionRef` 等中性字段；`dshSessionId` 降为 DSH adapter 内部 alias，映射必须
   携带 source/authority/valueOrigin。
3. 协议状态统一使用 `verified`、`known gap`、`known deviation`、`placeholder`、
   `deferred`、`not applicable`；本 Change 不凭文档补造 buyer 或未实现协议。

这些裁决已由 ADR-0044 和 `CONTEXT.md` 固化。

## 10. 第四轮裁决与剩余问题

Owner 已接受 P24 的启动 identity：`acceptedContractViewRef + contractGeneration`。
同一份具体已接受契约版本至多关联一个 Product Session / Primary Run；重复请求
返回已有关联、不重放动作。不能仅用版本数字或 launch request identity 去重。
该产品约束已记录到 ADR-0044 / CONTEXT；P24 的协议验证状态保持原有证据分类。

仍需审阅：

1. 已结束任务的“再做一次”与同一次运行的重发/恢复如何区分。
2. Runtime 创建 Product Session 的时机与“Session → Goal → Contract → Run”语义图
   的先后关系是否一致；恢复是否保留原 identity。
3. contract generation 的 scope、同一 Run 内契约修订与新 Run 的区分。
4. P1/P24/P25 的 correlation 由哪些原 Owner 发布，禁止以一个总 correlation owner
   接管 UniAgent 的 Goal/Contract authoring 或 Run Model 的 canonical state。

协议逐边矩阵按已接受范围记录 Producer / Consumer / Meaning / Minimal Payload /
Identity / Revision / Scope / Authority / Validity / Failure / Absence / Duplicate /
Late / Retry / Evidence / Lifecycle / Status；矩阵格式是审阅方法，不另设产品决策。

### 10.1 启动与恢复的代码事实（本轮只读核查）

| 检查面 | 当前实现事实 | 与裁决/基线的差距 | 状态 |
|---|---|---|---|
| P24 启动钥匙 | `ExecutionContractView` 只有 Version，无 accepted view ref/generation；`KernelRunDriver.Activate → UniKernel.ActivateGate` 只检查已有 RunView 与进程内 activation latch | 新裁决的 key 尚无类型、跨请求校验或持久关联 | known gap |
| 重复激活 | 同一 Kernel 返回同一 RunId；第二 driver 不获得执行 lease；终态 Drive 返回 AlreadyTerminal | 这是单 Kernel 实例行为，不证明跨进程/重启/并发提交的 key 规则 | known gap（完整 P24）；既有局部测试证据保留 |
| Contract revision | `RunModel.AdmitContract` 同 Version/预算签名重入复用 View；不同 Version 拒绝 | 尚无 ContractGeneration 或同 Run 契约 supersession 行为 | deferred（②）；实现是当前单 View 范围 |
| Runtime HTTP 创建 | `RuntimeRunStore.Create` 先随机分配 RunId/ProductSessionId，`HostRunner` 后以 RunId override AdmitContract | 代码把 Session/Run 记录和 Contract admission 混在一起；需要拆出 Task/Session admission、Agent contract、Run activation 三段 | known gap |
| Runtime HTTP recover | `/runs/recover` 按 launch/idempotency 返回已有 store snapshot，保留编号 | 不是 Kernel/World/RunModel/driver 状态恢复，不能宣称可继续动作 | known gap（执行恢复）；查回已有绑定行为单列 |
| Goal/Contract 发起链 | HTTP 路径由 HostRunner 构造 Contract，P25 Agent consultation 在 admission 后发生；没有显式 PrimaryGoal / UniAgent author Contract 的完整路径 | 与 UniAgent Goal/Contract authoring authority 的完整实现不闭合 | known gap；不得由 Host 接线反向改写 Owner |

代码证据落点：`src/UniClaw.Kernel/Run/RunModel.cs` 的 AdmitContract、
`src/UniClaw.Kernel/Run/ExecutionContractView.cs`、`src/UniClaw.Kernel/UniKernel.cs`
的 ActivateGate、`src/UniClaw.Kernel/Runtime/KernelRunDriver.cs` 的 Activate/Drive、
`src/UniClaw.Host/Runtime/RuntimeRunStore.cs` 的 Create/Recover、
`src/UniClaw.Host.Dsh/RuntimeHttpServer.cs` 的 StartRun 与 `/runs/recover`、
`src/UniClaw.Host/HostRunner.cs` 的 RunOnce admission 接线。此处来自当前代码盘点，
不是新增运行/恢复验证证据；Trace 仍禁止用于产品 Runtime 恢复（ADR-0013）。

### 10.2 第五轮裁决：Task → Instance/Session → Agent Contract → Kernel Run

Owner 已明确修正顺序：Runtime 根据 Task 创建 Task Instance，再分配 Product Session；
UniAgent 读取 Task 与 Session context，形成更细的 Execution Contract/协议并交给 Kernel；
Kernel 在 Contract View 被接受并合法激活后才创建/驱动 Primary Run。Product Session
因此先于 Primary Run 建立，但建立 Session 不等于开始执行。

同一次提交的重试复用已有 Task Instance/Session binding；任务结束后再次执行，创建
新的 Task Instance、Product Session 和 Primary Run。该顺序已写入 ADR-0045，并取代
前文把 Task Launch 当作 Task Instance 铸造者、把 Session 推迟到 Run 创建时的表述。

Owner 已确认“更细的协议”就是现有 `Execution Contract`；Kernel 接受后形成
`Contract View`，不新增同义协议对象。下一前沿是确定 P1 Contract admission、
P24 activation 与 P25 Consultation 的时序和最小载荷边界。

### 10.3 第七轮裁决：P1 / P24 / P25 的职责和身份字段

Owner 已冻结三条边的职责边界：

- P1 只提交和审核 Execution Contract；它可以关联 taskInstanceId、
  productSessionId、契约引用和版本，但不创建 Run、启动 Kernel 或产生 Effect。
- P24 只启动已经接受的 Contract View；启动钥匙由 acceptedContractViewRef 与
  contractGeneration 组成。同一钥匙的重试和并发后到请求返回同一个 Session/Run
  binding；契约内容不一致则拒绝；Run 已结束时只返回终态。
- P25 只在 Primary Run 已启动后的 decision boundary 做一次具体决策咨询；每个
  boundary 最多一次，同一 boundary 重试复用同一结果，不同 boundary 可以再次
  咨询。请求带 runId、productSessionId、Contract View 引用和本次 consultation
  correlation，不创建、修改或替换 Contract/Contract View。

三条产品协议只使用 host-neutral identity，不携带 `dshSessionId` 或 Host transcript
id。以上决定已落入 ADR-0046 和 CONTEXT；当前代码/协议载荷尚未完整实现这些字段，
因此仍按 known gap/deferred 记录，不把文档裁决当作 verified。

## 11. 本文不作的决定

- 不改变六个 Kernel Owner。
- 不把 Uni Kernel 变成所有状态的总 Owner。
- 不把 Memory、Core、Workspace、Harness 或 Provider 提升为 Product canonical authority。
- 不在逐边协议审阅完成前批量重写 P1-P26 的载荷或编号；已冻结的 P1/P24/P25
  Owner、identity 和 activation 规则除外。
- 不把当前未提交 `CONTEXT.md` 的新增词条自动视为已接受架构。
- Product Topology、Product Semantic Lifecycle、Product Architecture Baseline 三个术语已在 `CONTEXT.md` 同步；P25 次数语义已冻结，外围协议 buyer 仍待拷问。

## 12. 核心概念 Owner 初始矩阵

下表是第一轮静态审阅结果。`待裁决` 表示文档已有候选说法，但跨组件 owner、生命周期或实际 buyer 尚未完全闭合；不表示可以自行补实现。

| 概念 | 类型 | canonical owner / authority | 生命周期要点 | 当前状态 |
|---|---|---|---|---|
| Product Session | 关联根 | UniClaw Runtime 根据 Task 创建并分配；Task Launch 只提交/读取 binding；Host 只提供 Host Session | 先于 Primary Run，承载 Goal/Contract 形成，之后关联 Run/Outcome；不成为共享 mutable state | 方向已裁决；实现时序仍 known gap |
| Primary Goal | 产品目标对象 | UniAgent / Goal authority | Session 内创建；显式 revision；结束或不可继续时终止 | 产品代码已落地 |
| Goal Criterion | 目标判据 | UniAgent | 随 Goal 固定；被 Goal Evaluation 消费 | 产品代码已落地 |
| Execution Contract | 执行语义提案 | UniAgent authoring；Run Model admission | versioned；accepted version 不就地改写 | 已有类型与 P1 |
| Execution Contract View | 当前 Run 契约视图 | Run Model | admission 后 immutable；Run terminal 后冻结 | 已落地 |
| Primary Run | 一次执行生命周期 | Run Model 记录；Kernel 负责组合/激活 | legal activation 后 self-drive；单一 terminal | 已落地，activation 仍有边界细节 |
| Canonical Run State | Run 状态聚合 | Run Model | typed legal transition；terminal 后冻结 | 已落地 |
| Observation Proposal | 观察候选输入 | Capability/producer 产生；不拥有 admission | 一次 invocation/capture；可失败、迟到、部分 | 已落地；P2 |
| Canonical Evidence Record | 正式观察依据 | Evidence Ledger / Evidence Admission | append-only immutable；可 supersede/invalidate 引用 | 已落地；部分演化语义 deferred |
| WorldBelief Revision | 当前世界判断 | World Model / Reconciliation | 每次 reconciliation 新 revision；历史只读 | 已落地 |
| World State / Claim | Belief 内命题 | World Model | 随 revision 产生；无独立 truth lifecycle | 已落地；Core 映射候选 |
| Slice | scoped consumer projection | World Model 派生 authority | immutable；绑定 source revision/scope/freshness | 已落地；P4/UIWorld L4 |
| Control Intent | 控制意图 | Control Loop | 每个 cycle 形成；新 evidence 可使其失效 | 已落地；P8 |
| Tactical Hypothesis | 一次性控制假设 | Control Loop | 可废弃/替换；不得成为事实或授权 | 已落地；部分场景语义仍在 RUN-005 |
| Candidate Binding | 候选目标绑定 | Grounding Provider 产出 | 仅进入 Effect Boundary；未经确认无 effect authority | 已落地；P9 |
| Canonical Binding | 当前正式目标绑定 | Effect Boundary / Binding authority | 绑定 WorldBelief revision；currentness 变化后失效 | 已落地；P10 |
| Assurance Judgment | 动作/终局判断 | Assurance | immutable；输入或 freshness 改变后重判 | 已落地；P13/P16 |
| Effect | 期望施加的外部改变 | Runtime Effect semantic owner 与 Core candidate 的关系待闭合 | 不是执行成功结果 | 候选语义存在；跨 Core/Runtime 仍需审阅 |
| Attempt | 对 Effect 的一次尝试 | Effect Boundary / reliable execution record；Core 形状为候选 | 保存请求、binding、投递、外部执行、协调等不同轴 | 产品实现部分落地；Core/Runtime 边界需复核 |
| Effect Receipt | 投递留痕 | Effect Boundary | dispatch 后 immutable；不证明现实效果 | 已落地 |
| Verified Effect | 被 post-action Evidence 支持的效果 | Assurance judgment | 依赖 accepted post-action observation；不是 receipt | 已落地；部分 provider/origin gap |
| Proof Obligation | Contract/Run 级证明要求 | Run Model 记录；Assurance 判断 | 随 Run；terminal 时归档为 Outcome State | 已落地 |
| Outcome Proof | 终局证明判断 | Assurance | 只针对特定 terminal claim；证据不足不伪造分类 | 已落地 |
| Terminal Outcome State | Run 终局记录 | Run Model | exactly-once winner；terminal 后不可恢复 active | 已落地 |
| Runtime Outcome | 对外终局 envelope | Uni Kernel emission boundary | immutable、exactly once；发送后关闭 effect delivery | 已落地 |
| Goal Evaluation | 对用户目标的监督评价 | UniAgent | 消费 Runtime Outcome；不回写执行事实 | 已落地 |
| Durable Memory Record | 跨时间历史记录 | Memory System | versioned/retained/expired；不得直接成为 current truth | 声明占位，无产品实现 |
| Capability Definition | 产品能力协议 | Product T1 Capability Plane 管理面 | 稳定语义，不等于实现或实例 | 管理面已落地；各能力协议成熟度不同 |
| Capability Implementation | 能力具体实现 | Provider/Host/内部算法 realization | 可替换；不取得 Product authority | 已有多种实现 |
| Capability Binding | Host/Task 装配关系 | Composition Root / Host / Task scope | scope 内创建、关闭；不改 Definition | 部分已落地；task-scoped seam 有后续 gap |
| Capability Instance | 本次运行实例 | 当前 Host/Task scope | 绑定期有效；不回写全局 Registry | 现有全局解析，任务级实现未完全闭合 |
| Provider | 外部能力实现 | Provider 自身运行事实 | 经明确协议接入；不拥有 Product state | Perception provider 已有 component baseline |
| Product Skill | UniAgent 领域 SOP | Product/Profile owner | 初始化解析后固定；运行中不热切换 | manifest/loader 已有；语义接入需继续审阅 |
| Harness Skill | 开发过程技能 | Development Harness | 由 Harness catalog/UniFlow 消费 | 已有；与 Product Skill 必须分轴 |
| Product Tool | UniAgent 产品调用词汇 | UniAgent semantic owner；realization 负责暴露 | 不等同 Harness Tool | 语义与 DSH exposure 已分离 |
| Harness Tool | 面向 AI Coder/Workbench 的暴露单元 | `tool-registry.yaml` / Harness | 暴露元数据；不拥有 Product runtime state | 已有 registry/validator |
| Task | 可重复发起的工作定义 | Host/Task Catalog | 可产生多个 Task Instance | DSH Workbench 已有 adapter 形态 |
| Task Profile | 一次任务执行契约 | Product task/profile owner；编译到 Execution Contract | versioned；不复制 Agent Profile | 已有 `product/tasks/`；统一 Catalog 尚未闭合 |
| Task Instance | 一次任务执行关联 | UniClaw Runtime 根据 Task 创建；以 `taskInstanceId` 标识 | 创建时显式关联 Product Session；当前可一对一但 identity 分离 | 部分落地 |
| Host Session | 宿主承载会话 | Host realization；以 `hostSessionRef` 标识 | 显式关联 Product Session；不取得 Product authority | 已有 DSH 语义，字段中性化仍需审阅 |
| Workspace Snapshot | 查询结果组合 | Workspace Query Core projection | 记录读取时间/版本；不宣称全局原子一致性 | 已有 Query Core 实现 |
| Trace | 非权威诊断投影 | Trace store/source | 可缺失、迟到、重放；不参与恢复/事实判断 | 已有多类实现 |
| Core Record | 跨领域 typed shape | Product T3 Core surface；具体语义仍归原 owner | immutable projection/transport shape；无 Runtime authority | 候选模型，部分代码已存在 |

## 13. 协议逐边审阅摘要

本表把“协议语义是否成立”和“当前实现覆盖到哪里”分开记录。verified 只表示
已有代码与测试直接支持对应语义；没有把类型存在、旧文档声明或单进程行为扩大成
跨进程协议保证。

| 协议 | 责任边 | 当前结论 | 主要问题 |
|---|---|---|---|
| P1 | UniAgent → Run Model | admission 语义 verified；新身份字段 known gap | 现有最小载荷未完整携带 taskInstanceId/productSessionId；需要保持只收 Contract、不启动 Run |
| P2 | 观察/Attempt → Evidence Ledger | verified | 跨 Host transport 未纳入本轮；AttemptReport 仍只能作投递留痕 |
| P3 | Evidence Ledger → World Model | 语义已冻结；独立 origin 字段仍是 known gap | revision 演化与跨边界封装按既有协议处理；当前类型不能把 ObservationContext/Producer 组合冒充完整 origin 实现 |
| P4 | World Model → Control Loop | verified（in-process） | 必须保持 scoped view，不暴露 aggregate |
| P5 | Run Model → Control Loop | deferred（no current buyer） | 当前 Control 直接消费 Contract View + Slice；不要为了补齐编号创建空 DTO |
| P6 | Run Model → Control/Assurance | verified（in-process） | Contract View 只读，不能被 P25 就地改写 |
| P7 | Run Model → Assurance | 定义视图 verified；状态由 Assurance 独立投影 | P7 只保存“要证明什么”；ObligationStatus 不能反向成为 P7 的第二权威 |
| P8 | Control Loop → Assurance/Effect Boundary | 进程内 issuance verified | 跨 Host authority token 暂不设计；真实买方出现后再开协议变更 |
| P9 | Grounding Provider → Effect Boundary | verified | UI occurrence 与字符串 subject 是两种 realization，不是两种身份体系 |
| P10 | Effect Boundary → Assurance | verified | canonicalize 与 dispatch gate 的执法入口要保持唯一 |
| P11 | World Model → consumer views | verified（in-process） | 是按 consumer 派生的一族 view，不是共享 WorldBelief DTO |
| P12 | Evidence Ledger → Assurance | verified（只读投影） | 当前无独立跨进程 envelope；不能宣称 transport 已验证 |
| P13 | Assurance → Effect Boundary | verified | action-local judgment 只判断，不重复拥有 Binding/World authority |
| P14 | Effect Boundary → Capability Plane | verified（in-process） | DispatchRequest 不带 authorization；跨 Provider transport 仍是实现范围 |
| P15 | Capability Plane → Effect Boundary | verified | UnknownOutcome 必须保留，不能猜成功 |
| P16 | Assurance → Run Model | verified | 无 proof 时保持 non-terminal；null 不是一种终局分类 |
| P17 | Run Model → Uni Kernel | verified（单进程 exact-prior） | 分布式持久化 CAS 和恢复尚未验证 |
| P18 | Uni Kernel → UniAgent | envelope/emission verified | self-driven driver 与跨进程恢复后的继续执行仍是 known gap |
| P19 | UniAgent → user/session | producer verified；consumer 边未闭合 | Goal Evaluation 不得回写 Run/World；交付给谁、何时交付仍需外围审阅 |
| P20 | protocol objects → Memory | placeholder/deferred | 没有真实 buyer 和实现；不得把 Memory 当 canonical truth |
| P21 | Memory → authorized consumer | placeholder/deferred | recall 只能给 prior/context，不能直接建立 current-world claim |
| P22 | Effect flow → World Model | verified | 只作一次 association prior，不能变成 Evidence 或 identity |
| P23 | Effect/Obligation → World Model | code/test verified；协议基线状态陈旧 | baseline 仍写“无实现”；应改为 verified + 外部路径 known deviation |
| P24 | accepted Contract View → Kernel driver | 机械激活/同 Kernel 幂等 verified；完整协议 deferred/known gap | acceptedContractViewRef + contractGeneration、跨请求并发、持久恢复尚未实现 |
| P25 | Kernel driver → UniAgent | seam/fail-closed verified；per-boundary 规则已冻结 | 当前支持多边界多轮，但缺 productSessionId/Contract View ref 和同边界幂等；不允许创建或改写 Contract |
| P26 | sealed Trace → ScenarioStimulus | seal/import/fail-closed verified | 只属于测试/治理面；Trace 不能恢复 Product Runtime 或充当 command |

### 13.1 当前必须先处理的协议问题

1. P25 已明确为“每个决策边界最多一次”：同一 boundary 重试复用结果，不同
   boundary 可再次咨询。当前实现已有多边界调用，但 per-boundary correlation/
   idempotency 仍是 known gap。
2. P1/P24/P25 的中性身份字段和 P24 启动钥匙尚未进入实际类型、持久绑定和恢复
   路径；这属于 known gap，不能通过改文档标成 verified。
3. P23 的主协议文档落后于代码和测试，需要把旧的“无当前实现”标记改成代码范围
   与协议范围分列的状态。
4. P19–P21 和 P26 属产品 Runtime 外围或测试/治理面；外围审阅时要明确 buyer、
   交付面和禁止反向取得 Product authority 的边界。
5. P1 的“proposal 不建状态”要改成精确表述：未接受的 proposal 不产生 Run State；
   admission 成功后可以建立 accepted Contract View 和对应 Run 记录，但仍不激活。
6. P6/P7 需要把 Contract View 的不可变约束、obligation specification 和
   obligation status projection 分开；不能让 P7 变成 P16 的终局证明。
7. P8 当前用对象引用证明 Control Intent 的签发者，适合进程内 realization；
   若以后跨 Host 传输，必须另定义 authority token 或 adapter 映射，不能直接把
   序列化后的 intent 当作已签发。
8. P3 已冻结独立 origin 语义；实现仍需补字段和可执行断言，不能只凭
   provenance 中有 producer 名称或 Context 值就宣称语义闭合。

本表是同一 Change 的协议审阅底稿，不改变 P1–P26 编号，也不替代各协议的
canonical baseline；P25 次数语义已确认，后续按两段式范围继续审阅外围
Host/Workspace/Task/Capability/Provider/Profile/Launch/Harness 边界。

### 13.2 代码与测试证据索引

- P1/P6：src/UniClaw.Kernel/Run/RunModel.cs、src/UniClaw.Kernel/Run/ExecutionContractView.cs、
  tests/UniClaw.Kernel.Tests/RunIdentityTests.cs。
- P2/P3：src/UniClaw.Kernel/Evidence/ObservationProposal.cs、
  src/UniClaw.Kernel/Evidence/EvidenceLedger.cs、tests/ObservationIngressTests.cs。
- P4/P11：src/UniClaw.Kernel/World/WorldModel.cs、src/UniClaw.Kernel/World/Slice.cs、
  tests/WorldModelCanonicalOracleTests.cs、tests/FreshnessEnforcementTests.cs。
- P7/P13/P16：src/UniClaw.Kernel/Run/RunState.cs、
  src/UniClaw.Kernel/Assurance/RuntimeAssurance.cs、tests/TerminalOutcomeTests.cs、
  tests/EntityObligationFulfillmentTests.cs。
- P8：src/UniClaw.Kernel/Control/ControlLoop.cs、
  src/UniClaw.Kernel/Control/ControlIntent.cs、tests/ControlToEffectTests.cs。
- P9/P10/P14/P15：src/UniClaw.Kernel/Effects/EffectBoundary.cs、
  src/UniClaw.Kernel/Effects/EffectDispatch.cs、tests/Effects/EffectGateMatrixTests.cs。
- P17/P18/P24/P25：src/UniClaw.Kernel/Run/RunModel.cs、
  src/UniClaw.Kernel/Runtime/KernelRunDriver.cs、
  tests/UniClaw.Simulation.Tests/DeterministicScenarioTests.cs、
  tests/UniClaw.Simulation.Tests/DshSlowConsultTests.cs。
- P22/P23：src/UniClaw.Kernel/Effects/EffectBoundary.cs、
  src/UniClaw.Kernel/World/WorldModel.cs、
  src/UniClaw.Kernel/World/UiRealization/Continuity.cs、
  tests/UniClaw.Kernel.Tests/UIWorldContinuityTests.cs、
  tests/UniClaw.Kernel.Tests/EntityScopedObligationTests.cs。
- P26：src/UniClaw.Kernel/Trace/TraceModel.cs、
  src/UniClaw.Kernel/Trace/SealedTraceStore.cs、
  tests/UniClaw.Simulation.Tests/ScenarioImporterTests.cs。

这些证据主要是 deterministic/in-process 测试；live device、跨进程恢复和真实 Host
transport 仍保持各自的 known gap，不因本索引升级状态。

### 13.3 P1–P26 完整审阅矩阵

下面的矩阵把每条边按同一套问题展开：谁拥有语义、收到什么、产出什么、失败时
发生什么、重复/迟到/重试如何处理、恢复是否成立，以及当前实现到底验证到了哪一层。
`verified` 只表示对应语义有证据；跨进程、跨 Host、真实设备和完整 buyer 没有证据时，
仍保留 `known gap`、`known deviation` 或 `placeholder`。

| 边 | Owner（生产者 → 消费者） | 输入 → 输出 | 失败 / 缺失 | 重复 / 迟到 / 重试 | 恢复语义 | 当前实现状态 / 证据 |
|---|---|---|---|---|---|---|
| P1 Contract admission | UniAgent author → Run Model admission；Run Model 拥有接受权 | Execution Contract：version、objective、scope、effect 约束、proof criteria、可选 obligations、task/session 关联 → accepted Contract View 或拒绝 reason | 非法/不完整 proposal 拒绝；不建 Run State、不激活 | 同 version 重复返回同一 View；accepted View 不就地改写；不同 version 的替代规则 deferred | 只能重新提交/查询既有 View；不把 P1 当 Run resume | 进程内 admission verified；canonical UniAgent 跨边 producer、完整身份字段 known gap。`RunModel`、`ExecutionContractView`、`RunIdentityTests` |
| P2 Observation ingress | Capability Provider / Effect Boundary → Evidence Ledger；Ledger 拥有 admission | Observation/AttemptReport：claim、kind、origin、provenance、attempt correlation → admitted/rejected proposal result | 缺 kind、origin、provenance 或 correlation 时 fail closed；不直接产 Evidence | ingress 是 proposal；同一 attempt 重放由 Ledger admission 决定，不隐式补成功 | 只能重新提交原 proposal；无“从日志恢复 Evidence” | kind/context/attempt 语义 verified；独立 origin 字段仍 known gap。`ObservationProposal`、`EvidenceLedger`、`ObservationIngressTests` |
| P3 Evidence record | Evidence Ledger → World Model / P12 查询；Ledger 拥有 Evidence canonical state | admitted claim + kind/origin/provenance → immutable Evidence Record | 未 admission 无 record；查不到就是没有 backing，不猜测 | 同内容重放幂等复用；append-only；supersession/degrade deferred | 通过 Ledger 重查/重放同一 record；不能由 World Model 反向修复 | admission 与 identity verified；origin 随 record 的独立承载仍 gap。`EvidenceLedger`、`ObservationIngressTests` |
| P4 World slice | World Model → Control Loop；World Model 拥有 derivation/currentness | current revision、scope、freshness basis → scoped Slice | 无 current revision、scope 不可得或 revision 失效时无 Slice、下游拒绝 | Slice 是派生值；旧 revision 迟到后失效；消费方不能缓存成 current truth | 重新从最新 revision 派生；不从旧 Slice 恢复 World | in-process verified；`WorldModel`、`Slice`、`FreshnessEnforcementTests` |
| P5 Run snapshot | Run Model → Control Loop；Run Model 拥有记录 | objective status、run-level progress/obligation 状态 → run-side snapshot | 无 accepted Contract 时无 snapshot；下游 fail closed | typed transition 后派生；terminal 后冻结；重复读取无副作用 | 当前无 buyer；未来只能从 Run Model 当前状态重建 | deferred（no current buyer）；不造空 DTO，`EXP-008` |
| P6 Contract View | Run Model → Control Loop / Assurance；Run Model 拥有 canonical view | accepted Contract：version、objective、scope、effect/proof constraints → immutable Contract View | 未接受 Contract 无 View；消费方必须拒绝 | 同 version admission 复用；View 不可就地改写；迟到 proposal 不影响当前 View | 只读查询/重新投影同一 View；不由 P25 改写 | in-process verified；`ExecutionContractView`、`RunIdentityTests` |
| P7 Obligation definition | Run Model → Assurance；Run Model 定义，Assurance 判断 | obligation identity/kind/subject/required value/mandatory → definition view；Assurance 另产 status projection | 无结构化 obligation 时输出显式不可判定占位；不伪造满足 | definition 随 typed discharge 演进；状态重算不改 definition | 从 Run Model 重建定义；Assurance 重新计算状态 | definition view verified；status projection 分离已冻结，跨边 transport 仍 in-process |
| P8 Control Intent | Control Loop → Assurance / Effect Boundary；Control Loop 是唯一签发者 | intent identity、kind、effect class、target hint、basis revision → signed/issued intent | forged、未签发、terminal 后 intent 拒绝；不产生 Effect | 一次性 delivery 语义；迟到 intent fail closed；provider 不得自行 retry/replan | 当前只能在同一进程通过 issuance registry 判断；跨 Host token deferred | in-process issuance verified；跨 Host authority token deferred。`ControlLoop`、`ControlToEffectTests` |
| P9 Candidate binding | Grounding Provider → Effect Boundary；Effect Boundary 唯一认定者 | target subject/value、revision anchor、ambiguity → CandidateBinding/BindingDecision | stale、ambiguous、unknown target → reason；不建 CanonicalBinding、不进 Assurance | candidate 是 proposal；旧 revision 需重新 grounding；不得重复 dispatch | 重新基于当前 GroundingView 解析；不从候选恢复授权 | target seam verified；`EffectBoundary`、`EffectGateMatrixTests` |
| P10 Canonical binding | Effect Boundary → Assurance；Effect Boundary 拥有 binding | exact target、intent correlation、effect semantics、revision/freshness → CanonicalBinding | P9 拒绝则无 binding；跨 revision、已消费 binding 拒绝 | 同 binding 消费后不得二次 dispatch；judgment 拒绝不自动删除 binding | 只能重新 bind 当前 revision；是否允许同 binding re-judgment deferred | target/authority verified；`EffectBoundary`、`EffectGateMatrixTests` |
| P11 WorldBelief view | World Model → Assurance / Effect Boundary；World Model 拥有 Belief | revision/currentness、scoped claims、evidence refs、conflict、uncertainty、freshness → consumer-specific immutable view | 无 revision 或 scope 不可得时下游拒绝 | revision 单调演进；旧 view 只读且不再 current；不能缓存成平行 truth | 从当前 World Model 重新派生；Memory recall 不能恢复 current claim | in-process verified；`WorldModel`、`FreshnessEnforcementTests` |
| P12 canonical lookup | Evidence Ledger → Assurance；Ledger 拥有只读检索 | Evidence identity → canonical record（含 kind/origin） | 查无 record = 无 backing；Assurance 不猜测 | append-only 查询；迟到 record 只能按 admission/currentness 规则参与 | 重新查询 Ledger；不能由 Assurance 写回 Ledger | read-only projection verified；跨进程 envelope 未验证 |
| P13 Assurance judgment | Assurance → Effect Boundary Gate；Assurance 拥有 judgment | exact Intent + CanonicalBinding + revision/freshness → immutable judgment/reason | tuple 不匹配、freshness/安全检查失败 → 显式拒绝；P9 拒绝时无 judgment | 每个 exact tuple 一次；成员或 freshness 变更须重判；迟到 judgment 不授权别的 binding | 重新对 exact tuple 计算；不恢复旧授权 | verified，CBA-005；`RuntimeAssurance`、`EffectGateMatrixTests` |
| P14 Dispatch request | Effect Boundary → Capability Plane；Effect Boundary 拥有 delivery authority | bounded target、effect semantics/parameters、revision anchor → one dispatch attempt | terminal/delivery closed → 拒绝；请求不含 authorization | 单次 delivery；Capability 不得自行 retry/replan；迟到请求拒绝 | retry/recovery 由 Effect Boundary 的上层策略决定，不由 Provider 自行恢复 | in-process verified；跨 Provider transport 未验证 |
| P15 Dispatch result | Capability Plane → Effect Boundary；Effect Boundary canonicalize receipt | attempt outcome（三态）、reason、provider report/ref → EffectReceipt 或 UnknownOutcome | 缺失/不可解释 → UnknownOutcome；不得猜成功 | 每次 attempt 独立；UnknownOutcome 必须 re-observe，禁止 blind redispatch | 先重新观察/确认世界；不能把 UnknownOutcome 恢复成成功 | verified；`EffectBoundary`、effect gate tests |
| P16 Outcome proof | Assurance → Run Model；Assurance 是 proof authority | proof identity、classification、obligation status、basis/effect/situation refs、uncertainty → OutcomeProof 或 absence | 证据不足 = 无 proof、Run 保持 non-terminal；不伪造失败/成功 | immutable；同一 terminal proof 不重写；迟到 evidence 不直接改 proof | 继续收集证据后重新判断；不是从 null proof 恢复 terminal | verified；`RuntimeAssurance`、`TerminalOutcomeTests` |
| P17 Terminal outcome | Run Model → Uni Kernel；Run Model 唯一记录者 | OutcomeProof ref、classification、obligations、evidence refs、reason → single-winner OutcomeState | 竞争 transition、已 terminal、非法状态 → 拒绝 reason | 一个 Run 至多一次 terminal；late receipt/evidence 不改写、不二次 emission | terminal 不恢复 active；只能查询终态 | single-process exact-prior verified；分布式 CAS/恢复 gap。`RunModel`、simulation tests |
| P18 Runtime outcome | Uni Kernel → UniAgent；Kernel 唯一 emission boundary | terminal outcome semantics + proof provenance + reason → immutable RuntimeOutcome envelope | 无 terminal 无 envelope；emission 失败不得补发第二份 | exactly-once；重复消费不能回写 Runtime；terminal 后 Effect delivery closed | 当前进程内 envelope 可重建；跨进程 self-drive/continuation gap | envelope/emission verified；跨进程恢复 gap。`KernelRunDriver`、`DshSlowConsultTests` |
| P19 Goal evaluation | UniAgent → user/session；UniAgent 拥有 Goal Evaluation | goal/run correlation、satisfaction、disposition、criteria、rationale → immutable GoalEvaluation | 无 RuntimeOutcome 不评价；判据不可验证 = Undetermined | 同输入同 identity 幂等；迟到评估不得回写 Run/World | 可基于同一终态重新计算；不能恢复 Runtime 状态 | producer verified；consumer/user delivery 未来占位 |
| P20 Memory persist | Product protocol objects → Memory System；Memory 只保存副本 | 允许持久化的只读观察/reference → memory record | buyer/对象清单未定时保持 placeholder；失败不得影响 canonical state | 重复持久化不改变 Product truth；迟到副本不能覆盖 canonical state | 重新写入/重建副本；不得从 Memory 恢复 Evidence/Run/Binding authority | placeholder/deferred；无完整 buyer 和实现 |
| P21 Memory recall | Memory System → authorized Agent/Control/World consumer；Memory 无 truth authority | query/scope → prior/context/hypothesis/historical reference | recall 不可用返回 absence；不得作为 current claim | 重复 recall 是只读；迟到 recall 只能作 prior，不能覆盖 current revision | 重新 recall；current WorldBelief 仍需 P2/P3/P11 路径 | placeholder/deferred；non-evidentiary 约束已冻结 |
| P22 Runtime transition context | Runtime Effect Flow → World Model；World Model 只作 association consumer | actual attempt/effect kind、attempt correlation、epistemic strength → ephemeral association prior | 缺 correlation/非 actual 事实 → 不进入 association；不建 Evidence/identity | 单次消费、不可 append/cache；迟到 context 不能单独建立 container match | 重新从实际 effect flow 提供 context；不能从日志自动恢复 truth | code/test verified；`EffectBoundary`、`WorldModel`；外部 transport 仍 gap |
| P23 Continuity request | Effect/Obligation path → World Model；World Model 拥有 eligibility/adjudication | mode、target ref/descriptor、demand idempotency、source kind、revoke → GroundingView/ContinuityOutcome | 非法 source、scope 无法解析、revoke 冲突 → fail closed；Control/UniAgent 不得 mint demand | 同 demand key 幂等；revoke/continuity 状态按 typed transition；迟到 demand 不绕过 current revision | ResolveCurrent/ResolveContinuity 重新查当前 revision；不恢复旧 eligibility | code/test verified；外部 Effect Boundary physical path known deviation/gap |
| P24 Legal activation | Runtime activation path → Kernel internal driver；Runtime 提交，Kernel 消费 | acceptedContractViewRef、contractGeneration、可关联 task/session → Primary Run binding | 无 accepted View、key 内容冲突、terminal → 拒绝/终态；零新 Effect | 同 key 重复/并发复用同 binding；terminal 只返回终态；不允许 P25 改 Contract | 当前只有单进程 latch；跨请求/重启 recovery 尚未闭合 | mechanical latch verified；持久 key、并发、跨进程恢复 known gap/deferred |
| P25 Decision consultation | Kernel internal driver → UniAgent realization；Kernel 是唯一调用方 | run/session、Contract View ref、decision boundary、correlation、有界 context → advisory proposal/no-action/no-response | no-response、越权、correlation mismatch → fail closed；不改 Contract、不直接产生 Effect | 同一 boundary 可在 HardCap 内重复逻辑咨询；每次新咨询使用独立 correlation；同一逻辑咨询的重试复用 correlation/result；无 boundary 的迟到请求违规 | 当前 seam 可重新调用；同 boundary 跨进程结果持久化/恢复 gap | seam/fail-closed verified；identity、HardCap、独立 correlation、完整 owning-model 校验 gap |
| P26 Sealed trace import | Sealed Trace → Scenario Importer → Simulation Host；治理/测试面拥有 seal/import | sealed、integrity-checked Trace + virtual time/environment/agent decision → immutable ScenarioStimulus | 未 sealed、quarantined、integrity unknown → reject；Trace 不是 command | 同 artifact/version 导入应复用同 stimulus；运行中 trace/迟到 event 不可消费 | 可从 sealed artifact 重建模拟输入；不得恢复 Product Runtime/Effect grant | seal/import/fail-closed verified；simulation/governance only。`SealedTraceStore`、`ScenarioImporterTests` |

## 14. 外围边审阅：Task / Launch / Host / Workspace / Capability / Profile / Harness

第二段审阅确认了主链外的职责边界，但也找到几处会制造“双重身份”的真实实现
偏差。这里先记录目标规则和当前证据，不把目标规则误写成已完成实现。

### 14.1 Task、Session、Run、Host 的目标边界

| 对象 | 谁负责 | 人话解释 | 当前状态 |
|---|---|---|---|
| Task Definition / Task Profile | Task Catalog / Product declaration | 一个可重复发起的任务定义和它的执行约束 | Task/Profile resolver 尚未统一 |
| Task Instance | UniClaw Runtime | 这次真的要执行的任务编号 | Workbench 先造一套、Runtime 又造一套，known deviation |
| Product Session | UniClaw Runtime | 这次产品执行的总关联编号 | Runtime 会生成，但没有独立的 Task→Session 阶段 |
| Execution Contract | UniAgent author，Run Model admission | Agent 把任务翻译成 Kernel 能执行的边界 | 当前 HostRunner 直接代造，known deviation |
| Primary Run | Runtime activation path / Kernel | 真正开始跑的那次执行 | HTTP Store 先造 Run，再 admission/activation，known deviation |
| Host Session | Host / DSH realization | 宿主自己的一次承载会话 | Host 侧可生成；不能冒充 Product Session |
| Workspace | Workspace Query Core / adapter | 只读查询和投影 | authority 边界成立；host alias 仍可能出现在投影 |

目标顺序冻结为：

Task Definition/Profile → Runtime 创建 Task Instance → Runtime 分配 Product Session
→ UniAgent 形成 Execution Contract → P1 admission / Contract View → P24 activation
→ Primary Run self-drive → Host 绑定 Host Session。

### 14.2 当前必须处置的外围问题

1. **Workbench 双重 Task Instance**：Task Launch 页面先用本地编号写一条实例，
   Runtime 又生成另一条 taskInstanceId。修复方向是 Launch 只提交 Task/Profile 引用
   和启动钥匙，Runtime 返回唯一 binding，Workbench 只做投影，不再 newInstanceId。
2. **Runtime 允许外部传入 Product IDs**：LaunchRequest/Store 仍可接受并覆盖
   taskInstanceId/productSessionId。目标接口应禁止创建路径带这两个字段；只有恢复/
   查询已有 binding 时才能按 key 查回。
3. **HostRunner 代造 Contract**：当前 HostRunner 直接 new ExecutionContract 后
   调 Kernel admission。目标作者是 UniAgent；这条路径标 known deviation，不能靠
   Host 代码把它解释成 P1 已验证。
4. **Recover 的含义**：当前 Recover 只是按 launch/idempotency key 找回 snapshot，
   不是把 Kernel、World、driver 和 Host 现场接回去继续执行。对外应区分
   recovered binding/read-only 与真正 resume。
5. **Legacy instantiate**：旧 Host session 兼容路径可以保留，但必须明确为
   deprecated/Host-only，不能铸造 Product Session 或 canonical Task Instance。
6. **DSH alias 外泄**：dshSessionId 可以存在 DSH adapter 和 Host-source projection；
   进入 Product protocol 前必须转换为 productSessionId/taskInstanceId/runId/
   hostSessionRef，并保留 source/authority/valueOrigin。

### 14.3 Capability / Profile / Harness 的边界结论

- Capability Definition 属 Product T1 Capability Plane 管理面；Implementation 属
  Provider/Host realization；Binding 属 Composition Root/Task scope；Instance 属
  当前 Host/Task scope。四层是装配链，不新增 Product T2。
- Capability Registry 目前仍是全局实例解析，task-scoped Binding/Instance 尚未
  完整落地；这是 known gap，不能把全局 Resolve 当作任务级绑定。
- Provider 只产 raw output、health 和 realization 事实；Evidence、WorldBelief、
  Assurance、Effect、Outcome 仍由各自 Product owner 掌权。实际 perception 目录
  仍在 platforms/perception，属于 known deviation，不改变 owner。
- Product Profile / Product Skill 是产品声明资产；Harness Skill/Tool 是研发暴露
  面；Task Profile 不等于 Task Definition，也不等于 UniAgent Profile。统一
  Catalog/resolver 尚未闭合。
- Workspace、Harness、DSH 只能查询、投影、适配和绑定；不得铸造 Product identity、
  Contract、Run、Outcome 或 Effect authority。

这些外围发现已经回写到 Owner/known-gap 记录；外围协议矩阵见 §15。哪些属于本
Change 的文档修订、哪些必须另开实现 Change，按矩阵中的状态区分。

## 15. 外围协议矩阵摘要

| 边 | 生产者 → 消费者 | 这条边只做什么 | 当前状态 | 主要处置 |
|---|---|---|---|---|
| O1 Task Launch | Launch/UI → Runtime | 提交 Task/Profile 引用、启动钥匙和关联信息 | transport 有实现；identity 语义 known deviation | 禁止创建请求携带/覆盖 taskInstanceId/productSessionId |
| O2 Task/Session Binding | Runtime → UniAgent/查询面 | 返回 Runtime 唯一创建的 Task Instance + Product Session binding | 目标已冻结；当前无独立阶段 | 把 Store Create 拆成可观察 admission 阶段 |
| O3 Contract authoring | UniAgent → Kernel P1 | 用 Task + Session context 形成 Execution Contract | 当前 HostRunner 代造，known deviation | Host 不能成为 Contract author |
| O4 Legal activation | Runtime activation path → Kernel P24 | 用 accepted View + activation key 启动 Primary Run | 机械 seam 有；完整 identity/generation gap | 持久化 key、并发、终态、恢复需真实实现 |
| O5 Host attach | Runtime/Host → Host realization | 绑定 hostSessionRef，承载 transport/provider | attach 有实现；Host authority 边界 verified | dshSessionId 只留 adapter alias |
| O6 Recovery lookup | Runtime Store → Launch/Workspace | 按启动钥匙查回已有 binding/snapshot | read-only lookup verified；真恢复 known gap | 对外区分 recovered binding 与 resume |
| O7 Workspace projection | Workspace Core/adapter → consumer | 提供带 source/authority 的只读投影 | 边界 verified；旧 schema 仍强制 dshSessionId | 迁移为 hostSessionRef + source/authority/valueOrigin |
| O8 Capability Definition | Capability Plane → Composition Root | 描述能力、版本、作用域和依赖 | verified | 不新增 Product T2 |
| O9 Capability Binding/Instance | Composition Root/Task scope → Provider/Host | 把 Definition 解析为本次 Task 可用实例 | task-scoped binding known gap | 不把全局 Registry.Resolve 当作任务绑定 |
| O10 Profile/Skill load | Product declaration → UniAgent/Host adapter | 固定 Profile、Skill、manifest、revision | Product loader verified；Task Catalog resolver gap | Product Skill、Task Profile、Harness Skill 分轴 |
| O11 Harness exposure | Harness registry → Workbench/AI Coder | 暴露 Tool/Skill 调用形态和 posture | registry/validator verified | 不把 Harness Tool 当 Product Tool/Capability |
| O12 DSH identity mapping | DSH adapter → Workspace/Host projection | 把 Host 私有会话投影为 hostSessionRef | adapter boundary verified；旧 projection known deviation | Product protocol 禁止 dshSessionId |

这组 O 边是外围审阅摘要，不改变 P1–P26 编号；完整字段矩阵见 §15.1。O1/O2/O3/O4/O6/O7/O9
需要另开实现 Change，O5/O8/O10/O11 的主要语义已有证据，O12 需要 schema/fixture 迁移。

### 15.1 O1–O12 完整审阅矩阵

外围边不取得 Product Runtime 的 canonical authority。它们只负责把产品声明、
Host realization、Workspace 查询面和 Harness 暴露面接到主链；凡是需要创建、判断、
授权或终止的动作，都必须回到 P1–P26 的对应 Owner。

| 边 | Owner（生产者 → 消费者） | 输入 → 输出 | 失败 / 缺失 | 重复 / 迟到 / 重试 | 恢复语义 | 当前实现状态 / 证据 |
|---|---|---|---|---|---|---|
| O1 Task Launch | Launch/UI → UniClaw Runtime；Runtime 拥有 admission/binding | Task/Profile/TestSet refs、launch/idempotency/correlation、environment intent → launch result / Runtime binding request | ref 不存在、版本不符、key 冲突、调用方携带 Product IDs → fail closed；不创建半真 binding | 同一启动 key 必须复用同一 binding；不同 key 才是新提交；并发后到读同一结果 | 只允许按 launch/idempotency key 查询已有 binding；查询不等于 resume | transport 有实现；Workbench/Runtime 双重 Task Instance 与外部 ID 覆盖仍是 known deviation/gap。Task Workbench、Runtime HTTP |
| O2 Task/Session binding | UniClaw Runtime → UniAgent / Workspace query；Runtime 拥有两项 Product identity | Task ref + 合法启动请求 → taskInstanceId、productSessionId、显式 Binding | Task 不可用、Session 分配失败、已有 key 对应内容冲突 → 无 binding 或显式 partial，不从 Host 推导 | 同一提交重试返回同一 Task/Session；结束后再次执行必须新建；迟到 Host session 不得反铸造 | read-only lookup 可恢复 identity；真正 Task/Session admission recovery 尚未定义 | 目标已冻结；当前没有独立 admission 阶段，known gap |
| O3 Execution Contract authoring | UniAgent → Kernel P1；UniAgent author，Run Model admission | Task + Product Session context → Execution Contract / admission proposal | context 缺失、契约不完整、越权约束 → P1 拒绝；不建 Run、不激活 | 同一 version admission 幂等；不同 version 替代规则 deferred；Host retry 不是 lifecycle command | 重新读取 Task/Session 形成 proposal；不能从 Host transcript 恢复 Contract authority | 当前 HostRunner 直接代造，known deviation；P1 语义有进程内证据 |
| O4 Legal activation | Runtime activation path → Kernel P24；Runtime 提交，Kernel latch/driver 消费 | accepted Contract View ref + generation + activation key → Primary Run binding / terminal result | 无 accepted View、key 与契约内容不一致、已 terminal → 拒绝或返回终态；零新 Effect | 同 key 重复/并发复用；不同 key 不能覆盖旧 Run；late activation 不重启 terminal | 当前只支持单进程 lookup/latch；跨进程恢复、持久 key 和 resume 未闭合 | mechanical activation 有证据；完整 P24 known gap/deferred |
| O5 Host attach | Runtime/Host composition → DSH/Host realization；Host 只拥有 hostSessionRef | Product binding、host/environment intent → hostSessionRef + adapter attachment | handshake、设备、profile、transport 不可用 → attach failure；不得伪造 Product Session/Run | 同一 binding 重试复用已有 Host attach；迟到 Host 事件只能作 observation | 可查询已绑定 hostSessionRef；现场恢复/继续执行不是当前 read-only attach | attach、adapter boundary verified；dshSessionId 仅为内部 alias |
| O6 Recovery lookup | Runtime Store → Launch / Workspace query；Store 是 snapshot authority | launchId/idempotencyKey → existing binding/snapshot + status | 找不到、key 歧义、snapshot 损坏 → 明确 unavailable/corrupt；不猜测新 identity | 重复查询只读幂等；迟到查询不触发新动作；retry 仍使用原 key | 当前是 recovered binding/read-only lookup；不恢复 Kernel/World/driver/Host 现场 | lookup verified；真正 execution resume known gap |
| O7 Workspace projection | Workspace Query Core/adapter → UI/consumer；Workspace 只读投影 | canonical Task/Session/Run/Host refs + source/authority/valueOrigin → redacted project/session/trace/evidence view | source 不足、权限/查询失败、未关联 → 显式 unavailable/uncorrelated；不自动升级身份 | 重复读取不创建对象；迟到投影不能覆盖 canonical state；刷新重新查询 | 从 canonical owner 重新投影；Workspace 自身不保存可恢复 Runtime authority | 查询边界 verified；旧 schema 仍暴露 dshSessionId，迁移 gap |
| O8 Capability Definition | Product T1 Capability Plane management → Composition Root | Definition、version、scope、dependency、health contract → selectable capability description | 未注册、版本不兼容、health 不可用、scope 不符 → 不可选；不静默降级成别的能力 | 同 definition/version 解析应稳定；重复注册按 registry policy 拒绝或复用；迟到 health 只更新 realization | 重新从 canonical registry resolve；不从旧 Instance 推导 Definition | registry/definition 语义 verified；Capability owner 不新增 Product T2 |
| O9 Capability Binding / Instance | Composition Root / Task scope → Provider/Host realization；Composition Root 拥有 binding，Host 拥有 instance realization | Definition + task/session scope + configuration → explicit Binding + current Instance | 无 buyer、依赖/allowlist/health 不满足 → binding 失败；不能用全局 Resolve 冒充 task binding | 同一 task scope/version 复用 binding；不同 scope 不共享；迟到 provider health 触发重新判定 | 重新 resolve/rebind 当前 scope；不从 Provider transcript 恢复 Product capability authority | task-scoped Binding/Instance known gap；当前 registry 多为全局解析 |
| O10 Profile / Skill load | Product declaration → UniAgent / Host adapter；Product declaration 拥有内容，adapter 只加载 | Profile/Skill ref、revision、manifest、allowlist、hash → frozen scoped load + audit envelope | ref 不允许、hash/revision 不匹配、文件缺失 → fail closed；不加载任意 Harness Skill | 同一 revision 重复加载同一内容；运行中不热切换；迟到新 revision 只对新 Session 生效 | 重启按 ref/revision/hash 重载；不能从模型 transcript 推导 Profile/Skill | Product profile loader 有证据；Task Catalog/Profile resolver 尚未统一 |
| O11 Harness exposure | Harness registry → Workbench / AI Coder；Harness 拥有暴露元数据，不拥有 Product state | tool/skill entry、调用形态、consumer、posture、schema →可调用 exposure | registry 校验失败、schema/hash 漂移、能力未暴露 → unavailable/error；不伪造 Product capability | 同一 registry entry 查询幂等；调用重试由具体 Tool 协议负责；迟到输出不能改 Product truth | 重新加载 registry；不能从 Harness history 恢复 Product Run/identity | `tool-registry.yaml` 与 validator verified；Product/Harness 分离保持 |
| O12 DSH identity mapping | DSH adapter → Workspace / Host projection；DSH 拥有私有 alias，Product owner 拥有 neutral identity | dshSessionId + adapter source → hostSessionRef（host、sessionId、source/authority/valueOrigin） | 无显式 mapping、跨 session 误配 → uncorrelated/拒绝；不得从 transcript/目录推导 Product identity | 同一 mapping 重复返回同一 hostSessionRef；迟到 DSH event 只能挂 adapter projection | 重新查显式 mapping；不能用 dshSessionId 恢复 Product Session/Run | adapter boundary verified；旧 Workspace schema/fixture 外泄 dshSessionId，known deviation |

O 边的共同恢复规则是：只有 O6 可以做只读 binding/snapshot lookup，O5/O7/O10
可以重建 realization 或投影；O1–O4、O8–O9、O11–O12 都不能借“恢复”名义重新
铸造 Product identity、Contract、Run、Outcome 或 Effect authority。
