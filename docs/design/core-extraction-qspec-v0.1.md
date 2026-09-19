# CORE-001 — UniClaw Core 语义协议提取 to-spec

> DocumentType: `CORE_EXTRACTION_TO_SPEC`
>
> Status: `LOCKED v0.1 — CORE SEMANTIC BASELINE`
>
> Lock Scope: Core 架构边界与责任归属、禁止依赖、单一事实权威、语义基线与逐条引用关系（§10）。
> 不冻结：最终字段、继承树、包名、存储布局；不宣称源码迁移完成。
>
> Provenance: PRE-SPEC 对齐归纳（core-extraction-document-alignment-v0.1.md，2026-09-18）
> → to-spec 起草（DRAFT / REVIEW_REQUIRED）→ 引用审查与验收对照通过 → 锁定（CORE-001 收尾，2026-09-18）。
> 后续修订必须通过变更记录与反例说明（D7）。
>
> Authority: `NONE（锁定语义基线由 Governing Change CORE-001 记录承载，文档 Authority 不升级——锁定≠冻结，范围见 Lock Scope）`
>
> Scope: Core 语义协议的候选 to-spec 与提取验收，不授权源码迁移。
>
> Governing Change: `changes/CORE-001/state.md`
>
> Prerequisite: [Core 提取 to-spec 前置文档归纳与引用基线](core-extraction-document-alignment-v0.1.md)

## 1. 规范目的

建立一个可供浏览器、手机、机器人和仿真世界依赖的最小候选 Core 协议。协议只固化跨领域语义、责任边界、历史约束和使用关口；不把当前 UI 实现整体搬入，也不提前冻结字段、继承树、包名、序列化或物理存储。

引用：

- `/Users/fran/Documents/Code/best-fran/best-fran/uni-claw/UniClaw_Core_Model_设计文档_vNext.1.md` §1–§4；
- `/Users/fran/Documents/Codex/UniClaw_Core_Model_当前结论粗版.md` §1–§3、§10–§15；
- `docs/design/core-extraction-document-alignment-v0.1.md` §1–§4。

## 2. Core 边界

```text
Core = Specification + World + Effect

Specification
└── Clause

World
├── Segment
│   └── Slice（条件性局部观察结构）
├── Evidence
├── Claim
└── Event（发生语义保留，独立表示待验证）

Effect
└── Effect
    └── Attempt
        └── TargetBinding
```

状态：同步既有结论。

引用：vNext.1 §3、§5–§8；对齐指南 §2、§5–§7；验证证据 §1、§3。

说明：六种核心记录是语义基线，不要求形成六个独立类、表或持久化对象。`Event` 的发生语义必须保留，但能否由 Evidence/Claim/Revision/Runtime 无损表达，仍由后续反例验证。

禁止外推：该结构不是三个基类、数据库大对象、全局事务或最终继承树。

## 3. Core 必须保持的语义

| 规则 | 状态 | 来源 |
|---|---|---|
| 要求和权限不能由观察内容自动产生。 | 已确认原则 | vNext.1 §3.3、Clause 章节；对齐指南 §2.3 |
| 判断不等于现实；投递不等于结果；记录编号不等于现实身份。 | 已确认原则 | vNext.1 §4、Effect 章节；对齐指南 §3、§7 |
| 未知不自动解释为当前、可信、已授权、成功或失败。 | 已确认原则 | vNext.1 §4.5、§7；验证证据 §3 |
| 历史依据不能被 latest、当前 canonical 或新版本静默改写。 | 同步既有结论 | vNext.1 §4.1、§4.4；对齐指南 §3、§5、§7；验证证据 §3 |
| 每类正式记录有明确维护归属，不建立第二套独立事实。 | 已确认原则 | vNext.1 §3.2、§10；`docs/architecture/product-architecture-baseline-l0-l3.md` §1；`docs/design/product-architecture-migration-baseline.md` §2 |

## 4. World 语义

### 4.1 Segment

`Segment` 提供跨观察的持续引用，但不承诺已经正确识别现实身份。改名、移动、重渲染或重新定位不能仅凭编号决定同一性。

状态：同步既有结论。

引用：vNext.1 §6.1–§6.3；对齐指南 §5.1；场景库文件身份、UI 重渲染与机器人持续性场景；验证证据 §3。

### 4.2 Slice

`Slice` 表达某个 Segment 或 World 范围在一次观察中的局部记录、覆盖范围、时间上下文和可用于历史绑定的依据。Slice 可以重叠、包含或重复覆盖；新观察不覆盖旧 Slice；未观察到不自动等于不存在。

用于历史绑定的 Slice 必须固定保存，或能够按相同依据可靠重建。是否独立持久化为实体，保持待验证。

状态：同步既有结论 + 本轮补充规则。

引用：vNext.1 §6.4–§6.10；对齐指南 §5.2、§7；`src/UniClaw.Kernel/World/Slice.cs`；`tests/UniClaw.Kernel.Tests/UIWorldGroundingSeamTests.cs`；验证证据 §3、§6。

### 4.3 Evidence 与 Claim

`Evidence` 保留来源、时间、处理链和上下文；`Claim` 表达基于依据形成的判断、冲突、不确定性和演化关系。证据入库可以触发评价，但不自动表示命题被采纳。

状态：已确认原则。

引用：vNext.1 §6.11–§6.22；对齐指南 §6；`tests/UniClaw.Kernel.Tests/EvidenceToBeliefTests.cs`；`tests/UniClaw.Kernel.Tests/ClaimEvolutionTests.cs`。

### 4.4 Event

六种核心语义记录中的 `Event` 位置必须保留。发生语义、事件时间、参与者、次数和因果判断必须能够表达；但当前材料尚未证明必须建立独立 Event Core 记录。先由 Evidence、Claim、Revision/Runtime 记录表达并保留验证缺口。

状态：待验证，不得冻结为独立 Core 类型。

引用：vNext.1 §6.15–§6.18；审阅附件；验证证据 §3、§6；场景库因果、延迟、迟到和更正场景。

## 5. Effect 语义

`Effect` 表达逻辑操作；`Attempt` 表达一次实际投递、进展、外部执行判断和协调结果；`TargetBinding` 表达当次目标、定位方式、固定依据和有效条件。三者不得由一个混合生命周期字段替代。

绑定不强制使用 `segmentId + sliceId + worldVersion + freshness`，也不要求全局 World 版本。稳定记录引用、固定快照引用和现实身份判断必须分开。

状态：同步既有结论。

引用：vNext.1 §7；对齐指南 §7；`src/UniClaw.Kernel/Effects/EffectDispatch.cs`；`tests/UniClaw.Kernel.Tests/ControlToEffectTests.cs`；`tests/UniClaw.Kernel.Tests/DispatchSeamSpecificationTests.cs`。

## 6. 领域扩展与下层减法

浏览器、手机、机器人和仿真是 Core 的 realization 或 Adapter。Core 不反向依赖 UI 树、DOM、ADB、浏览器会话、设备 SDK、机器人 SDK、识别库或 Harness Session。

公共职责进入 Core 后，下层可以删除重复类、字段和校验；不要求旧类保留或与 Core 一一对应；混合职责模型可以拆分或组合；只有语义真正相同且共同约束成立时才考虑继承。

状态：同步既有结论。

引用：对齐指南 §10、§13；vNext.1 §10；`docs/design/core-extraction-document-alignment-v0.1.md` §4–§5。

## 7. 第一条 tracer bullet

```text
手机页面观察
→ Evidence
→ Page Segment
→ Slice S1
→ 滚动
→ Slice S2
→ 元素 Claim
→ Effect
→ TargetBinding
→ Attempt（包含实际点击时间）
→ 点击后新 Evidence
→ 重新评价 Claim / 页面状态
```

必须证明：页面持续引用、多 Slice 历史、元素局部观察、绑定固定、点击时间归属 Attempt、Unknown 不升级为成功、点击后结果由新 Evidence/Claim 评价。

状态：本轮补充规则。

引用：场景库 UI/滚动与异常场景；`tests/UniClaw.Kernel.Tests/UIWorldGroundingSeamTests.cs`；`tests/UniClaw.Kernel.Tests/ControlReferencePolicyTests.cs`；验证证据 §3、§6。

## 8. 双向验证门槛

在最终公共 API 冻结前，至少需要：

1. 一个 UI/手机 realization 通过第一条 tracer bullet；
2. 一个非 UI realization（机器人小场景或确定性仿真）表达持续目标、局部观察、固定绑定、实际投递和未知结果；
3. 两者不依赖同一套 UI 类名、字段或隐含假设；
4. 反例能够定位应修改 Core、realization、Adapter 或指南的责任层。

状态：本轮补充规则。

引用：Grill Q6、Q9、Q13；`docs/architecture/uniagent-realization-baseline-v0.1.md`；审阅附件关于最小候选基线的结论。

## 9. 待验证与阻塞范围

| 问题 | 状态 | 阻塞范围 |
|---|---|---|
| 独立 Event 是否不可替代 | 待验证 | 只阻塞需要独立事件计数/同一性/复杂因果的用途 |
| Slice 独立持久化与可靠重建 | 待验证 | 阻塞依赖历史 Slice 的持久绑定和完整页面回放 |
| 旧模型最终映射 | 待验证 | 阻塞源码迁移，不阻塞候选协议边界 |
| 第二种非 UI realization | 待验证 | 阻塞公共 API 冻结，不阻塞 to-spec 候选文本 |
| 最终字段、继承树、包名、存储布局 | 未进入本轮 | 阻塞实现设计，不阻塞语义 to-spec |

## 10. 完成与锁定

to-spec 通过审查后，任务收尾应锁定：

- Core 架构边界和责任归属；
- 禁止依赖和单一事实权威；
- 语义基线：术语、定义、适用边界和有效条件；
- 本 to-spec 的逐条引用关系。

锁定动作不得宣称源码迁移完成，也不得把推演、自检或测试计划写成产品测试通过。后续反例必须通过变更记录修订锁定基线，并保留旧结论与新证据的关系。
