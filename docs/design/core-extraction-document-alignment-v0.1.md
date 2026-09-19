# UniClaw Core 提取：to-spec 前置文档归纳与引用基线

> DocumentType: `CORE_EXTRACTION_DOCUMENT_ALIGNMENT`
>
> Status: `PRE-SPEC / CANDIDATE`
>
> Authority: `NONE`
>
> Date: `2026-09-18`
>
> Scope: Core 语义协议提取的文档对齐，不授权源码迁移，不冻结最终字段、继承树、包名或存储结构。

## 1. 用途

本文是 to-spec 的前置输入。它归纳现有材料的权威层级、已确认结论、当前实现证据和待验证问题。
它不是新的架构裁决，也不替代已冻结的领域协议。

to-spec 中的每条重要规则必须回指本文列出的来源，并注明来源章节、路径或验证证据。没有来源的新主张必须标记为“本轮补充规则”或“待裁决”。

## 2. 依据层级

发生冲突时按以下顺序处理：

1. 用户明确确认的 vNext.1 修订和后续审阅结论；
2. Core 抽取指南的对齐修订稿；
3. 场景枚举和仓库测试，作为语义压力与验证证据；
4. 当前源码结构，作为现状证据，不作为最终架构裁决。

仓库中的候选或草案文档不能自行升级为权威。文档的 `Status`、`Authority` 和适用范围必须随引用保留。

## 3. 主要来源清单

### 3.1 用户提供的外部材料

| 来源 | 用途 | 当前地位 |
|---|---|---|
| `/Users/fran/Documents/Code/best-fran/best-fran/uni-claw/UniClaw_Core_Model_设计文档_vNext.1.md`（第 3、4、5、6、7、8、9、10、11、12 章） | Core 目的、三模型边界、Clause、Segment、Slice、Claim、Event、Evidence、Effect、Attempt、Binding、Runtime 配套契约 | 主要设计依据；与后续明确修订冲突时以后续修订为准 |
| `/Users/fran/Documents/Codex/UniClaw_Core_Model_当前结论粗版.md`（第 0–15 节及附录） | 已整理的抽取规则、Gap 对照、上提与下层减法、验证关口 | 对齐修订稿；作为 to-spec 结构输入 |
| `/Users/fran/Documents/Codex/UniClaw_场景枚举库_v0.1_补充异常.docx` | 128 个主场景与 80 个异常场景，提供跨 UI、文件、网络、支付、通信、机器人、因果、身份、时间和结果的语义压力 | 验证材料，不直接决定模型数量 |
| `/Users/fran/.codex/attachments/52556104-b098-472c-8962-d63ce40813bc/pasted-text.txt` | 审阅修订：最小候选基线而非严格最小、Event 不独立冻结、Slice 条件性保留、删除测试须覆盖演化保持 | 后续明确审阅结论 |

### 3.2 仓库内架构与设计材料

| 来源 | 用途 | 当前地位 |
|---|---|---|
| `docs/architecture/product-architecture-baseline-l0-l3.md`（文档头、§1） | Product 的 Evidence → WorldBelief → Control/Effect → Verification/Outcome 分层与 authority 边界 | `CANDIDATE_FOR_ADOPTION / L0-L3_CLOSED`，`Authority: NONE`；用于边界参考，不授权 Core 迁移 |
| `docs/architecture/uworld-protocol-baseline-l4.md`（§0–2） | UIWorld 是 UI 领域 realization，负责 canonical world belief，不负责控制、安全和任务完成 | `FROZEN v0.3` 的 UI 领域协议；不能直接当作跨领域 Core |
| `docs/design/runtime-flow-simulation-core-model-v0.1.md`（§1–4） | Runtime 闭环、Fast/Slow、Evidence admission、World reconciliation、Slice、Effect、Verification 和 Trace 边界 | `DRAFT / REVIEW_REQUIRED`；候选设计，不升级为 Core 权威 |
| `docs/design/product-architecture-migration-baseline.md`（§1–3） | 迁移必须保持单一 canonical owner、单一 authority，不以类存在或删除作为完成标准 | `CANDIDATE / NOT_ADOPTED`；用于后续迁移方法，不定义 Core |
| `docs/design/legacy-to-target-architecture-mapping.md` | 旧结构到 Target 的职责映射方法 | 迁移参考；不用于反推 Core 类型一一对应 |
| `evidence/2026-09-18-core-minimal-candidate-validation.md`（§1–5） | 场景删除测试、表达保持、演化保持、现有 Kernel/Simulation 测试结果和剩余缺口 | `SUPPORTED_WITH_GAPS`；验证证据，不是严格最小性证明 |

## 4. 已确认的语义基线

以下内容可进入 to-spec 的“已确认原则”，但仍需逐条带来源：

1. Core 的顶层边界是 `Specification + World + Effect`。
2. 当前基线保留六种核心语义记录：`Clause、Segment、Evidence、Claim、Event、Effect`。六种语义记录不等于六个独立类型；`Event` 的独立表示仍待验证。
3. `Slice` 是 `Segment` 下的条件性局部观察结构；滚动、多视口、历史绑定和覆盖不足场景要求它的语义不能删除。它是否必须是独立持久化实体，尚未裁决。
4. `Attempt` 和 `TargetBinding` 是 `Effect` 内不可折叠的执行职责，分别表达实际尝试和当次绑定。
5. 要求和权限不能由观察内容自动产生；判断不等于现实；投递不等于结果。
6. 历史依据、固定快照和现实身份判断必须分开；历史绑定不能悄悄跟随 latest 或当前 canonical。
7. `Evidence` 保留来源、时间、处理链和上下文；入库不自动表示命题被采纳。
8. `Event` 的发生语义必须保留，但独立 Event Core 记录尚未通过删除测试，不在本轮冻结。
9. Core 不依赖 UI、设备 SDK、识别库或 Harness；浏览器、机器人和仿真通过 realization/adapter 使用 Core。
10. 旧类不要求一一对应；允许上提、删除、合并、拆分、替换、继承、组合和引用。

## 5. 当前实现事实

当前 `src/UniClaw.Kernel` 同时包含 Evidence/Claim/Revision、UIWorld continuity、Slice projection、TargetBinding、Effect dispatch、Runtime 和 Trace 等职责。以下类型只能作为提取时的现状证据：

- `EvidenceRecord`、`WorldBeliefRevision`、`Claim` 相关类型：Core 候选或 World realization 候选，需检查 UI 与 Runtime 耦合；
- `Slice`：已有局部投影实现，但历史 Slice 序列和持久化边界尚未被完整验证；
- `UiEntityModel`、`ContainerAssociation`、UI occurrence、DOM/ADB/native locator：UI realization 或 Adapter；
- `EffectDispatch`、驱动、Receipt、Runtime Outcome、Trace：Effect/Runtime/Harness 边界，不能直接作为现实事实；
- `WorldModel`：混合职责，不能整体搬入 Core。

## 6. 验证状态与缺口

已完成一次有界验证：Kernel 语义测试 94/94 通过，Simulation Async Perception 测试 15/15 通过；均有环境性 `NU1900` 警告，未影响执行。结果只支持当前最小候选基线，未证明严格最小性。

进入 to-spec 时必须保留以下缺口：

- Event 的事件同一性、计数、复杂因果和延迟归属；
- Slice 的独立持久化与可靠重建边界；
- 多视口页面覆盖、元素出现时间和点击时间线的完整端到端证据；
- Core 候选记录与旧类型的最终映射；
- 第二种非 UI realization 对公共 seam 的反向验证。

缺口只阻塞依赖相应保证的用途，不阻塞整个 Core 边界规范。

## 7. to-spec 引用与完成规则

to-spec 每条规则至少包含：

- 规则正文；
- 状态：已确认原则 / 同步既有结论 / 本轮补充规则 / 待验证 / 待裁决；
- 来源文档与章节、仓库路径或测试证据；
- 适用范围与禁止外推；
- 缺少保证时的阻塞范围。

本轮任务完成后，才锁定：

- Core 架构边界与责任归属；
- 禁止依赖与单一事实权威规则；
- 语义基线（术语、定义、适用边界、有效条件）；
- to-spec 的来源引用关系。

锁定不等于冻结源码字段、继承树、包名、数据库布局或迁移已经完成。后续变更必须有明确的变更记录、来源和反例说明。
