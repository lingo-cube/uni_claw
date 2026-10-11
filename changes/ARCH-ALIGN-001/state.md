# ARCH-ALIGN-001 — UniClaw 产品架构与协议整体对齐审阅
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: def7f0fc08847be0d2b9a42bfa42651d30a73691

## Intent（WHAT/WHY）

对当前 UniClaw 产品架构、核心概念、Owner/Authority、生命周期、实现落点、
架构文档和跨组件协议做一次整体审阅，消除 L0-L3、Core、Task/Profile/Contract、
Capability/Tool/Skill、Product/Host/Harness 等语义面的歧义。

当前 L0/L1 已发现结构性混用：同一组组件、生命周期对象和事实记录被放在同一层
级描述。若直接继续修协议，会把尚未解决的架构边界固化到协议载荷中。

## Scope

In：

- 现行产品架构基线、实现基线、协议基线、ADR、CONTEXT 和代码 README 的一致性审阅；
- L0-L3 层级、产品组件、产品语义对象、Kernel 责任域和 Core 候选模型的重新归类；
- Owner、Authority、canonical state、lifecycle、projection、adapter 和实现状态矩阵；
- 当前实现与已接受架构的偏差、文档元数据冲突、候选/冻结/占位状态的区分；
- 形成至少两套架构归类方案及其取舍，供 Owner 选择；
- 架构方案确定后，按同一 Owner/Authority/lifecycle 规则整体审阅跨组件协议；
- 方案被接受后的文档、索引、协议和必要代码同步。

Out：

- 在架构和协议方案被 Owner 接受前迁移产品代码；
- 以实现现状反向覆盖已接受的产品语义；
- 把 Workspace、Development Harness、DSH adapter 或 Provider 提升为 Product authority；
- 将候选 Core 模型、Memory 占位或未验证 buyer 直接宣布为冻结产品能力；
- 顺手清理当前工作树中与本 Change 无关的修改。

## Current Findings

- 产品基线 §3 的 L0 同时包含组件（UniAgent、Uni Kernel）和语义对象（Goal、Contract、Run、Evidence、Outcome）。
- 产品基线 §5 又把 UniAgent、Uni Kernel、Capability Plane、Memory System 定义为 L1，造成组件层级重复。
- `src/UniClaw.Agent/README.md` 与 `src/UniClaw.Kernel/README.md` 也把 Agent/Kernel 视为 L1 peer。
- Core 当前是候选的跨领域不可变记录/投影形状，不是新的运行时 Owner；不能直接与 UniAgent/Uni Kernel 并列。
- 现有协议基线依赖上述层级，但其真正约束主要是 Owner/Authority/输入输出/生命周期，不应由层号单独推导。
- 仓库中还存在多个未命名空间化的 L0/L1/L2/L3 轴：产品架构层级、Capability 继承/协议层级、感知 Provider Plane 层级、决策粒度层级、Profile 装配层级和 Harness 资产分级；同一个 `L0` 在不同文档中含义不同。
- 产品架构基线头部仍为 `CANDIDATE_FOR_ADOPTION / Authority: NONE`，但 `AGENTS.md` 将其列为 GREENFIELD 产品代码的直接权威，且 E2B-001/README 以 L0-L3 CLOSED 使用；这是文档元数据与消费规则冲突。
- 协议基线已声明 canonical 且有 verified/known gap/placeholder/deferred 等状态，但协议文件名仍直接使用产品 L1-L3 层号；UIWorld L4、Perception Plane L0-L2、Decision Granularity L0-L2 也各自复用层号。
- `CONTEXT.md` 当前存在未提交修改；审阅必须把工作树词汇与 HEAD 中已提交的产品事实分开，不得将未提交内容自动视为已接受架构。

## Decisions（当前仅记录已确认方向）

- 2026-10-11 Owner 裁决：采用 `Product T0–T3` 拓扑轴；T0=系统边界与外部参与者，
  T1=UniAgent/Uni Kernel/Capability Plane/Memory System，T2=Kernel 六责任域，
  T3=内部模型、协议实现和 realization 模块。
- 2026-10-11 Owner 裁决：Goal、Contract、Run、Evidence、WorldBelief、Outcome、
  Evaluation 进入独立 Product Semantic Lifecycle / Evidence-Belief flow，不再作为
  Product T0/T1 的同类组件。
- 2026-10-11 Owner 裁决：旧 Product L0–L3 只保留历史引用和迁移映射；Capability、
  Decision、Profile、Harness、Protocol 等层级必须带命名空间。
- 2026-10-11 Owner 裁决：产品架构基线升级为 `ADOPTED / PRODUCT-T0-T3-CLOSED`，
  `Authority: PRODUCT_ARCHITECTURE_BASELINE`，并通过 ADR-0042 固化。
- 2026-10-11 Owner 裁决：Core 保持 Product T3 的跨域 immutable record/projection
  正交面，不进入 Product T1/T2，不取得 Runtime authority。
- 2026-10-11 Owner 裁决：Task Instance、Product Session、Primary Run、Host Session
  四个 identity 分离；当前可一对一，但只能通过显式 Binding 关联，禁止从目录、
  transcript 或 Host session 推导。
- 2026-10-11 Owner 裁决：Capability Definition / Implementation / Binding / Instance
  分别归 Product T1 Capability Plane 管理面、Provider/Host realization、Composition
  Root/Task scope、当前 Host/Task scope；不新增 Product T2。
- 2026-10-11 Owner 裁决：协议采用同一 Change 的两段式审阅，先 Product Runtime
  主链，再 Host/Workspace/Task/Capability/Provider/Profile/Launch/Harness 外围边。
- 2026-10-11 Owner 裁决：UniClaw Runtime 根据 Task 创建 `taskInstanceId` 并分配
  `productSessionId`；Host 只铸造 `hostSessionRef`，UniAgent 只消费/监督；Kernel
  在 Contract View 接受并合法激活后才创建/驱动 `runId`。
- 2026-10-11 Owner 裁决：跨边界只使用 host-neutral identity；`dshSessionId` 降为
  DSH adapter 内部 alias，映射披露 source/authority/valueOrigin。
- 2026-10-11 Owner 裁决：协议状态统一为 verified/known gap/known deviation/
  placeholder/deferred/not applicable；本 Change 不凭文档补造 buyer 或未实现协议。
- 2026-10-11 Owner 裁决：P24 启动钥匙采用 `acceptedContractViewRef +
  contractGeneration`，识别一份具体已接受契约版本；同一 key 至多关联一个 Product
  Session / Primary Run，重复请求返回已有关联且不重放动作。协议验证状态不因本裁决升级。
- 2026-10-11 Owner 修正时序：Runtime 根据 Task 创建 Task Instance，再分配 Product
  Session；UniAgent 读取 Task + Session context 形成细化 Execution Contract/协议；
  Kernel 在 Contract View 接受并合法激活后才创建/驱动 Primary Run。任务结束后再次
  执行创建新的 Task Instance、Product Session、Primary Run；同一次提交重试复用 binding。
- 2026-10-11 Owner 裁决：UniAgent 形成的“更细协议”统一命名为 Execution Contract；
  Kernel 接受后形成当前 Run 的 Contract View，不新增同义协议对象。
- 2026-10-11 Owner 裁决：P1 只提交/审核 Execution Contract，P24 只启动已接受的
  Contract View，P25 只在 Primary Run 已启动后的 decision boundary 做一次具体咨询；
  P25 不创建或改写 Contract/Contract View。
- 2026-10-11 Owner 裁决：P1/P24/P25 只使用 taskInstanceId、productSessionId、
  runId、Contract View reference、contractGeneration 和 consultation correlation
  等中性字段；不带 DSH session id。同一启动钥匙的重复/并发请求复用同一 binding，
  同一钥匙但契约内容不同则拒绝，已终止 Run 只返回终态。
- 2026-10-11 Owner 裁决：P25 的“一次具体决策”按 decision boundary 解释；每个
  boundary 最多一次，同一 boundary 重试复用同一结果，不同 boundary 可以再次咨询。
- 2026-10-11 Owner 裁决：P3 单独记录 observation origin 语义，尤其区分
  post-action effect flow 观察与外部观察；ObservationContext 和 producer provenance
  继续保留，当前代码缺独立 origin 字段，记录为 known gap。
- 2026-10-11 Owner 裁决：P7 拆成 Run Model 的 obligation definition view 与
  Assurance 的 obligation status projection；P8 暂保留进程内 issuance 验证，不为
  尚无真实买方的跨 Host 场景预造 authority token。

- 本 Change 先做架构审阅和方案比较；Owner 已选择 Product T0–T3 与独立语义轴，
  现在进入概念 Owner、Core/Workspace/Host 边界和协议前置的下一轮拷问。
- 架构确定后才审阅和修订协议；协议不得先于 Owner、Authority、lifecycle 和 canonical state 冻结。
- 产品语义对象与架构组件必须分开描述；Core、Workspace、Harness、Host/DSH realization 的边界必须单独标注。
- 层级编号必须带命名空间或改用不会与产品架构层级冲突的术语；决策粒度、Capability maturity、Profile assembly 和文档证据级别不能裸用 Product `L0`。
- 任何现状与目标架构的差异都要标记为已落地、部分落地、声明占位、候选、冻结或待裁决，不能用代码存在代替语义接受。

## Alternatives to compare

1. **系统上下文优先**：L0=UniClaw 与 User/Host/Environment；L1=UniAgent、Uni Kernel、Capability Plane、Memory System；L2=Kernel 六责任域；L3=内部模型与协议。
2. **产品内部拓扑优先**：L0=UniAgent、Uni Kernel、Capability Plane、Memory System；产品语义对象另列为生命周期模型；L1=组件职责；L2=Kernel 六责任域；L3=内部模型与协议。
3. **双轴模型**：保留一条组件拓扑轴，不再把 Goal/Contract/Run/Evidence/Belief/Outcome 强行编号；另建产品语义生命周期图和协议图。

## Acceptance

| # | 判据 |
|---|---|
| A1 | 形成当前架构全景图：组件、语义对象、责任域、Core、Host/Workspace/Harness、实现落点和外部边界可分别定位 |
| A2 | 每个核心概念都有 canonical owner、authority、生命周期、输入/输出和禁止越权；同义词和跨语境词被标出 |
| A3 | Product T0-T3 方案至少包含两套可比较方案、取舍和 Owner 待决点；选定方案后不再存在同一层混用 |
| A4 | 架构基线、CONTEXT、README、实现状态和 ADR 的冲突形成可追踪处置表；候选/冻结/占位不混淆 |
| A5 | 架构方案接受后，协议审阅覆盖现有 P1-P26、外围 O1-O12 及 UIWorld L4 边，逐边给出 producer、consumer、输入投影、输出、版本、失败/迟到/重复语义和生命周期；当前实现状态单独标注 |
| A6 | 最终文档与代码同步只在明确授权范围内进行，所有验证提供 method/expected/actual/evidence |

## Constraints

- 以上游已接受产品基线、协议基线和 ADR 为约束；冲突先裁决，不绕行。
- Uni Kernel 仍是 UI 领域运行时；泛化只由真实 buyer 驱动。
- Evidence、WorldBelief、Run State、Assurance Judgment、Canonical Binding、Runtime Outcome 各自保持单一事实/判断权威。
- Workspace 是只读观察面；Development Harness 是研发控制面；Host/DSH 是 realization/adapter；Provider 不取得 Product authority。
- 保护当前工作树中与本 Change 无关的已修改、未跟踪和生成文件。

## Verification

```yaml
verification:
  level: CONTRACT
  method: "读取架构/协议/ADR/CONTEXT/README/代码索引并建立概念、Owner、生命周期和实现状态矩阵"
  expected: "所有核心边界可追溯；L0-L3 混用、同义词冲突和未裁决项被显式列出"
  actual: "完成十轮架构裁决并补齐 P1–P26（26 条）与 O1–O12（12 条）完整审阅矩阵：每条边均记录 Owner、生产者/消费者、输入/输出、失败/缺失、重复/迟到/重试、恢复语义和当前实现状态；Product T0–T3、独立语义生命周期、Core T3 正交面、四身份显式 Binding、Capability 四层 Owner、Runtime Task→Instance→Session 时序、Execution Contract 命名、P1/P24/P25 分工、中性 identity、启动幂等规则、P25 每边界一次、P3 origin 独立语义、P7 定义/状态分离和 P8 跨 Host 凭证延后已写入基线/词汇/ADR；P23 陈旧状态和外围身份偏差按代码范围保留；未修改产品实现代码"
  evidence: "docs/analysis/uniclaw-architecture-protocol-overall-review-2026-10-11.md §13.3/§15.1；docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md；docs/adr/0042-product-topology-t0-t3-and-semantic-lifecycle.md；docs/adr/0043-core-identity-capability-and-protocol-review-boundaries.md；docs/adr/0044-runtime-session-identity-and-protocol-status.md；docs/adr/0045-runtime-task-instance-session-and-agent-contract-sequence.md；docs/adr/0046-p1-p24-p25-contract-activation-consultation-boundary.md；docs/adr/0047-protocol-origin-obligation-and-issuance-boundaries.md；git diff --check"
```

## Status log

- 2026-10-11 · UNDERSTAND → RESOLVE · Owner 要求对当前项目做整体架构/协议审阅；先核对现有权威、Change State、脏工作树和层级冲突。
- 2026-10-11 · RESOLVE（进行中） · 已确认 L0/L1 层级混用；Owner 已选 Product T0–T3、
  独立语义生命周期和 ADOPTED 基线权威；待完成 Owner/Boundary 与协议前置拷问。
- 2026-10-11 · RESOLVE（第一轮交付） · 建立 Authority: NONE 的整体审阅底稿、三套架构重排方案和核心概念 Owner 初始矩阵；DocsMetadata 4/4 通过；未修改既有产品架构、协议或代码。
- 2026-10-11 · RESOLVE（第一轮裁决） · 已落 ADR-0042、更新 Product baseline 元数据/拓扑与语义轴说明、同步 CONTEXT 词汇；未改 P1–P26 载荷或产品代码。
- 2026-10-11 · RESOLVE（第二轮裁决） · 已落 ADR-0043、同步 Core/四身份/Capability Owner/两段式协议范围；待进入 Product Runtime 主链协议逐边拷问。
- 2026-10-11 · RESOLVE（第三轮裁决） · 已落 ADR-0044、同步 Runtime identity 铸造权、中性字段和协议状态分类；下一轮进入 P24 activation 与 P1/P24/P25 correlation。
- 2026-10-11 · RESOLVE（第四轮裁决） · Owner 接受按具体已接受契约版本识别启动；
  已同步 ADR-0044 / CONTEXT，正在核对恢复、generation scope 与 Session 创建时序。
- 2026-10-11 · RESOLVE（第五轮裁决） · Owner 修正为 Runtime 先创建 Task Instance/Session，
  UniAgent 再形成细化 Contract，Kernel 最后创建/激活 Run；已落 ADR-0045，当前实现
  的 HTTP 创建顺序记录为 known gap。
- 2026-10-11 · RESOLVE（第六轮裁决） · “更细协议”统一为 Execution Contract，已同步
  CONTEXT/审阅底稿；下一轮处理 P1 Contract admission、P24 activation 与 P25 Consultation
  的时序和最小载荷边界。
- 2026-10-11 · RESOLVE（第七轮裁决） · 已落 ADR-0046，固化 P1/P24/P25 的分工、
  中性 identity、启动钥匙的重复/并发/终态语义；开始准备 P1–P26 逐边审阅矩阵。
- 2026-10-11 · RESOLVE（协议首轮盘点） · 已完成 P1–P26 与 UIWorld 边界的只读盘点：
  P1–P18 的主要进程内语义、P22/P23/P26 的实现证据、P20/P21 的占位状态，以及
  P24/P25 的身份/恢复/次数缺口已写入审阅底稿；当时 P25 次数语义仍待 Owner
  确认，已由第八轮裁决解决。
- 2026-10-11 · RESOLVE（第八轮裁决） · Owner 确认 P25 为每个 decision boundary
  最多一次；同边界重试复用结果、不同边界可再次咨询。已同步 ADR-0046、
  CONTEXT、协议基线和审阅矩阵。
- 2026-10-11 · RESOLVE（第九轮裁决） · Owner 确认 P3 增加独立 origin 语义；
  已同步 P2/P3 协议基线和审阅矩阵，代码字段仍保持 known gap。
- 2026-10-11 · RESOLVE（第十轮裁决） · Owner 确认 P7 定义/状态分离、P8 暂不做
  跨 Host 凭证；已同步协议基线、审阅底稿和 Owner 矩阵。
- 2026-10-11 · RESOLVE（命名迁移） · 已把 UniAgent/UniKernel 公共 README 中的
  裸 L1/L2/L0-L3 改为 Product T1/T2/T3 或正交 Product T3 surface；其他局部
  L0-L4 继续保留其命名空间，不再作为 Product 架构名称。
- 2026-10-11 · RESOLVE（外围首轮盘点） · 已确认 Task Launch/Runtime 存在两套
  Task Instance 身份、Runtime 接口允许外部覆盖 Product IDs、HostRunner 代造
  Contract、Recover 仅查回 snapshot，以及 Workspace 旧 schema 仍暴露 dshSessionId；
  均记录为 known deviation/gap，未把 adapter 投影冒充 Product identity。
- 2026-10-11 · RESOLVE（外围矩阵） · 已将 O1–O12 的 Task/Session/Host/Workspace/
  Capability/Profile/Harness 边写入审阅底稿；本 Change 保留边界和状态，后续实现
  Change 再处理 Runtime 分阶段创建、Workbench 双编号、Workspace schema 迁移和
  task-scoped Capability Binding。
- 2026-10-12 · RESOLVE（完整矩阵） · 已在审阅底稿补齐 P1–P26 与 O1–O12 的 Owner、输入/输出、失败、重复/迟到/重试、恢复和实现状态字段；本轮仍只改文档和协议，不改产品代码。
- 2026-10-12 · VERIFY → CLOSED · 已核对 26 条 P 边和 12 条 O 边顺序完整、矩阵列数一致、已知缺口均有状态标记，`git diff --check` 通过；无阻塞裁决，Change 收口。
