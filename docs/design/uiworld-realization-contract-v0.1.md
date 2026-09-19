# UI World Realization Contract v0.1（草案）

> DocumentType: `UIWORLD_REALIZATION_CONTRACT`
>
> Status: `DRAFT / REVIEW_REQUIRED`（CORE-015 Step 2 交付，等待评审；
> 评审通过后进入 Step 3 双向验证设计）
>
> Authority: `NONE`（契约语义经评审后由 Change State + 上游 Core 契约承载）
>
> 上游: `UniClaw.Core/CoreRecords.cs`（候选语义基线）·
> `core-extraction-qspec-v0.2.md`（to-spec 审阅版）·
> `core-extraction-uiworld-alignment-v0.1.md`（对齐表）
>
> 框架（Human 2026-09-19 裁决）：**Core 管核心世界模型抽象；UI World
> 管相关领域实现细节补充。** Core 契约约束 UI realization 必须保留的
> 语义；UI realization 自持 UI-specific identity 与观察算法。不是
> Core → UI 反向投影。

## 1. 定位

UI World realization 是「按 Core 契约实现的第一个领域 realization」：
它把 accepted Evidence 变成带 UI 领域结构的版本化世界判断，并向消费方
（grounding / binding / assurance）提供 UI 形态的消费面。Core 契约
定义它**必须保住**的语义；本契约定义它**自己拥有**的领域细节。

当前物理形态：`src/UniClaw.Kernel/World`（WorldModel 及其类型族）。
本契约是语义分层文件；物理拆分是 Step 4 决策，不因本契约成立而自动
触发搬移。

## 2. 输入（UI World 只消费，不拥有）

| 输入 | 来源 | 边界 |
|---|---|---|
| accepted Evidence（EvidenceRecord） | Evidence Ledger admission（P2/P3） | UI World **不做第二套 admission**；未 admit 的观察不进 Reconciliation |
| previous WorldBelief revision | 自身历史 | revision 只读、append-only |
| UI observation strategy 输入 | 组合根注入（如 StatefulObservationStrategy / ReplayFrameObservationStrategy） | 算法可替换；输出语义不变 |
| continuity demand / association context（P22/P23） | EffectTargetCommitment / EntityScopedObligation | non-evidentiary：只买 eligibility/prior，不建 identity、不产生 revision |

## 3. 输出（UI World 拥有并保证的领域结构）

| 输出 | Core 对应（投影方向 UI→Core） | 必须保住的 Core 语义 |
|---|---|---|
| immutable WorldBelief revision（含 WorldState claims、Conflict、Evidence Basis、FreshnessBasis） | Evidence / Claim | 单一写入路径（Reconciliation）；冲突显式保留双方 evidence，不静默覆盖；claim 可溯源到 evidence |
| Container + ContainerGraph（revision-bound 关系 belief） | Segment | 持续同一性由 association evidence 建立，非自报；graph relation 不是永恒结构 |
| revision-local ObservationOccurrence | Slice.ObservedRecordIds | occurrence 引用只在源 revision 作用域内有效；provider node/bbox/OCR 只是 evidence |
| demand-gated LogicalItem continuity | —（UI 自有连续性层） | Ended 仅来自正面 lifecycle evidence；Ambiguous/Insufficient ≠ Ended |
| UI Slice（revision 派生 scoped 只读消费面） | Slice | 来源 revision 明确、依据 evidence basis 可列、观察时间与处理版本不混同 |
| BindingView / GroundingView 等 consumer view | TargetBinding 的 realization 侧依据 | owner 唯一派生、ephemeral、不携带 consumer judgment（ADR-0011） |
| UI-specific 历史引用（历史 revision、旧 occurrence/locator 引用） | BasisReference（非 Slice 固定依据，未来非 UI realization 用） | 历史保留；序列化保存后仅为历史记录，不充当有效 target handle |

## 4. 不得拥有（负向清单）

1. Clause / Permission authority——观察内容永不自动产生授权（Core 结构
   性保证在 UI 侧同样成立）。
2. Effect authorization / Effect Gate / Driver delivery——binding 依据
   供给（BindingView），不执法、不投递。
3. Goal evaluation——世界判断不评价目标。
4. Trace truth——诊断面零供给、零依赖。
5. 第二套 Evidence admission——只消费 admitted evidence。
6. 可靠执行记录——执行源归 EffectBoundary/Runtime（ADR-0023）；UI
   World 只经消费面供给 grounding/binding 事实。
7. 跨 Run 恢复真相——pending 是 advisory 资料（CORE-014 Q2），UI World
   不从旧 Run 记录推断当前世界。

## 5. Core 契约条目 → UI realization 义务映射

| Core 条目（CoreRecords / qspec） | UI realization 义务 |
|---|---|
| `Segment`（持续引用） | Container 承担持续引用；ContainerIdentity ≠ detection/OCR/DOM identity；投影 `ProjectSegment(containerId, semanticKind)` |
| `Slice`（revision 下局部观察；观察依据列表化） | UI Slice 从 revision 派生；`ObservationEvidenceIds` = revision Evidence Basis；`ObservedAt` = FreshnessBasis.AsOf；Coverage 如实（当前 "partial"） |
| `Evidence`（观察依据） | 透传 admitted EvidenceRecord 的 provenance 全字段；不增删语义 |
| `Claim`（命题 + disposition + evidence basis） | WorldClaim → `Predicate` 开放（当前投影只用 observed-value——UI 侧 identity/事件/因果命题是 Step 3 验证项，不新增 Core 对象）；Conflict disposition 映射 `ClaimDisposition.Conflict` |
| `Effect` / `Attempt` / 三轴 ExecutionState | UI World 义务止于供给 binding/grounding 事实；Attempt 字段（请求快照/执行端/授权依据/三轴）由执行源（CORE-013）在 Runtime 侧承载——`ProjectAttempt` 增强是 Step 3 验证项 |
| `TargetBinding`（Canonical ∧ 固定依据才可 dispatch） | UI binding 依据 = revision/Slice（`HasFixedBasis` 满足路径）；stale 判定 = revision currency 派生，无 event；旧 binding 不自动换目标 |
| `CoreInvariants.IsTerminalDelivery`（Completed/Failed 才终局） | Unknown 投递不进世界判断为成败；Unknown 唯一后继 re-observe |
| Event 无独立类型（CORE-003） | 发生/归属/因果以 Claim 组合表达；UI 侧不建第二事件真相 |

## 6. UI 自留领域细节（Core 不约束实现方式）

- identity 铸造与判别算法（association 四值 Matched/New/Ambiguous/
  Insufficient 的判定过程）；
- observation strategy 选择与组合（stateful/replay 等）；
- locator 形态（spatial frame / native key）与 grounding 算法；
- continuity adjudication（SameReferent/Ambiguous/Insufficient/
  Contradicted）与 demand 生命周期；
- revision 内部布局与持久化形态。

## 7. 验收钩子（→ Step 3 双向验证，不在此实现）

双向：① realization → Core projection 语义保留；② Core 约束 →
realization 满足检查（Core 不自动生成 UI identity）。至少覆盖对齐表
§7 所列 7 场景（滚动后局部观察、多 Slice 重叠历史保留、Occurrence
revision-local、LogicalItem 跨 revision 连续、旧 Binding 不自动换目标、
Unknown 不变成成败、非 Slice 固定资源版本不被伪装成 Slice）。

## 8. 本契约不冻结

程序集名、继承树、最终类签名、存储布局、物理拆分方式（Step 4 三选项
另行裁决）；`Predicate` 词汇集；Attempt 三轴与 journal 状态的映射语义
（Step 3 验证后定）。
