# Core 提取 × UI World realization 对齐表 v0.1

> DocumentType: `CORE_EXTRACTION_REALIZATION_ALIGNMENT`
>
> Status: `DRAFT / READ-ONLY ALIGNMENT`（只读对齐——不改代码、不建新类、
> 不冻结程序集名/继承树/类签名）
>
> Authority: `NONE`
>
> Change: `CORE-015`
>
> 目标框架（Human 2026-09-19 定格）：**提取 Core 模型为顶层抽象契约；
> UI World 相关模型作为第一个按该契约实现的 realization。**已有方向是
> UI → Core 投影（`CoreSemanticProjection`，可表达性证明）；目标方向是
> Core 契约约束 UI realization 必须保留的语义，UI realization 自持
> UI-specific identity（Container/Occurrence/LogicalItem）与观察算法。
> **不是** Core → UI 反向投影——Core 不凭空制造 UI identity、DOM
> locator、Occurrence 或 LogicalItem。

## 1. 目标分类（语义 Owner + realization；分类先行，不立即建程序集）

| 分类 | 职责 | 当前物理位置 |
|---|---|---|
| `UniClaw.Core` | 跨领域语义协议：Clause / Segment / Evidence / Claim / Effect；Slice / Attempt / TargetBinding 的共同约束 | `src/UniClaw.Core`（已存在，候选基线） |
| `UniClaw.UIWorld`（目标） | UI World realization：Container、revision-local Occurrence、LogicalItem continuity、UI Slice、UI locator / grounding、UI-specific reconciliation | `src/UniClaw.Kernel/World`（**语义已区分、物理未分层**：`WorldModel` 同时承担通用 reconciliation 与 UI 全套） |
| `UniClaw.Kernel` | 产品运行时与 Owner 协调：Evidence admission、World orchestration、Control、Assurance、EffectBoundary、ReliableExecutionSource、Run lifecycle | `src/UniClaw.Kernel`（已存在） |
| `UniClaw.Perception`（目标） | 感知与输入适配：screenshot / OCR / DOM / ADB / browser / vision provider | `src/UniClaw.Kernel/Perception` 等（现状按 owner 分，未按本表分） |
| `UniClaw.Agent` | Goal / evaluation / Agent decision | `src/UniClaw.Agent`（已存在） |
| `UniClaw.Harness / Simulation` | Scenario / replay / deterministic fixture / test oracle / trace projection | `tests/UniClaw.Simulation.Tests`（已存在） |

判断：不能因 `WorldModel` 含 UI 字段就把整个 Kernel WorldModel 原样搬成
UI Core；也不能因分类存在就立即创建程序集——分类是裁决归属的框架，
物理拆分是 Step 4 决策。

## 2. Core 候选语义（契约面）现状

- 基线：`qspec v0.2`（= **to-spec** 审阅版，`CORE_EXTRACTION_TO_SPEC_
  REVIEW`；CANDIDATE / REVIEWED，Review Lock CORE-007，语义修订
  CORE-008）→ 落地为 `src/UniClaw.Core/CoreRecords.cs`（文件头自述
  "不冻结最终字段/继承树/包名/存储布局"）。
- 结构性保证已具备：Core 无任何从 Evidence/Claim 导出 Clause 的 API
  （观察内容永不自动产生授权）；`CoreInvariants` 已编码
  HasFixedBasis / CanDispatch（Canonical ∧ 固定依据）/ IsTerminalDelivery
  / 基据稳定性。
- **关键事实（本轮核verified）：常被当作"Core 缺口"的三个投影缺口，
  Core 候选类型的字段已全部备好（CORE-006 落）**：
  - `Attempt.RequestSnapshotId / ExecutorId / AuthorizationBasis /
    ExecutionState`（DispatchProgress × ExternalExecutionStatus ×
    CoordinationStatus 三轴）——候选字段已存在；
  - `TargetBinding.BasisReferences`（`BasisReference(ReferenceId, Kind,
    SnapshotKey)` 非 Slice 固定依据）——候选字段已存在；
  - `Claim.Predicate` 自由谓词——字段天然开放。
  因此**不存在需要立即新增的 Core 对象**；缺口都在 realization
  projection 侧（§4）。

## 3. Kernel UI World（`WorldModel`）已有能力

revision 历史（`WorldBeliefRevision`）、Container association
（ContainerGraph）、revision-local Occurrence、demand-gated LogicalItem
continuity、Slice 与 stale Binding 判定（revision currency 派生）、
Evidence → WorldBelief 单一写入路径（Reconciliation）、owner 派生
consumer view（BindingView / ActionAssuranceView / GroundingView）、
grounded dispatch 消费链。滚动/局部观察/点击绑定/未知投递等场景已经
现有 Core projection 测试通过（`evidence/2026-09-19-core-*`）。

判断：UI World 作为 Kernel 内现实实现已具备较完整能力；缺的不是能力，
是**独立 realization 契约**（Core 约束 ↔ UI 职责的显式文档，Step 2）
与物理分层（Step 4）。

## 4. `CoreSemanticProjection` 覆盖矩阵（UI → Core 方向）

| 投影方法 | 覆盖 | Core 字段已备 | 备注 |
|---|---|---|---|
| ProjectClauses | ✓ | — | objective→Requirement、criteria→Criterion；Permission 不自动制造（结构性正确） |
| ProjectEvidence / ProjectSegment / ProjectSlice / ProjectEffect / MapDelivery | ✓ | — | Slice.Coverage 固定 "partial"（词汇单值，记为小注） |
| ProjectClaim | 部分 | ✓（Predicate 自由） | Predicate 硬编码 `observed-value`；UI identity / 事件同一性 / 因果类命题未投影 |
| ProjectAttempt | 部分 | ✓（四字段已备） | 只填 Id/Effect/Binding/StartedAt/Delivery/DeliveryEvidenceId；**未填** RequestSnapshotId / ExecutorId / AuthorizationBasis / ExecutionState 三轴。数据源现已存在：CORE-013 执行源 `ExecutionRegistration`（请求固定值 / ExecutorId / AdmissionNote）+ journal 状态可映射三轴（映射语义属 Step 2/3 设计，此处不冻结） |
| ProjectTargetBinding | 部分（UI 语义正确） | ✓（BasisReferences 已备） | `basisSliceId` 为必填参数——对 UI 正确（UI binding 依据即 revision/Slice）；BasisReferences（非 Slice 固定依据）未投影，属**未来非 UI realization 的投影义务**，非当前 UI 缺口 |

## 5. 缺口归属分类

| 缺口 | 归属 | 处置 |
|---|---|---|
| Core 对象缺口 | **无**（§2 关键事实） | 不新增 Core 类型 |
| Attempt 四字段 / Claim 谓词未投影 | UI realization projection 缺口 | Step 2/3 契约与双向验证中定映射；数据源已具备（CORE-013） |
| 独立 UI World realization 契约缺失 | **主要缺口**（realization 契约层） | Step 2：输入/输出/不得拥有清单 + Core 约束映射 |
| WorldModel 通用+UI 物理混合 | 物理分层缺口 | Step 4 三选项裁决（Kernel 内组合 / 独立模块 / 仅拆 domain types），不提前冻结 |
| 非 UI realization（文件/API/机器人） | 未开始 | 仅 Core 可表达性验证过；其投影义务（BasisReferences 等）随各自 realization 建立 |
| Effect 执行源 | Runtime 责任，**已完成**（CORE-013 / ADR-0023） | 与 UI World 抽取正交，不混入本线 |

## 6. 文档漂移清单（本次同步修正）

| 位置 | 漂移 | 修正 |
|---|---|---|
| `qspec v0.2` §7（L165-166） | CORE-009 契约仍标 `CHANGES_REQUIRED` | 实际：契约 APPROVED；CORE-011 仿真证明、CORE-013 产品实现已闭环（ADR-0023）——本次更新 |
| `docs/design/README.md` L14 | 把契约文件错标为「CORE-010 受控实现规划 / PLAN / REVIEW_REQUIRED」 | CORE-010 规划实体是 `plans/2026-09-19-core-010-reliable-execution-record.md`（已 APPROVED 且经 CORE-011/013 执行）——本次修正 |
| `docs/design/README.md` | 5 篇文档漏登记（qspec v0.1、document-alignment、responsibility-matrix、archive-skeleton、replay-roadmap） | 本次按各自自述状态补登 |
| `uniagent-dual-realization-architecture-v0.1.md` L638 | `CHANGES_REQUIRED` 字样 | 非漂移——历史评审结果记录（2026-09-11 SOL review），保留 |

## 7. 后续步骤（每步独立 Gate，不并步）

- **Step 2 — UI World realization 契约草案**（文档）：输入（accepted
  Evidence / previous revision / UI observation strategy / continuity
  demand）、输出（immutable revision / Container graph / Occurrence /
  LogicalItem / UI Slice / GroundingView / 历史引用）、不得拥有清单
  （Clause/Permission authority、Effect authorization、Driver delivery、
  Goal evaluation、Trace truth、第二套 Evidence admission）+ Core 契约
  条目 → UI 职责的逐条映射。评审后进入 Step 3。
- **Step 3 — 双向验证设计**（不迁移）：realization → projection 语义
  保留检查 + Core 约束 → realization 满足检查（不自动生成 UI
  identity）；至少覆盖滚动后局部观察、多 Slice 重叠历史保留、Occurrence
  revision-local、LogicalItem 跨 revision 连续、旧 Binding 不自动换
  目标、Unknown 不变成成败、非 Slice 固定资源版本不被伪装成 Slice。
- **Step 4 — 物理拆分决策**：三选项（Kernel Owner 内组合 UI
  realization / 独立 UI World 模块 / 仅拆 UI-specific domain types）；
  仅在契约与双向验证完成后裁决。
