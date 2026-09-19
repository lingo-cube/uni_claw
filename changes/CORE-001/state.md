# CORE-001 — Core 语义协议提取 to-spec
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

**WHAT**：基于已归纳的设计文档、场景库、审阅结论和仓库验证证据，形成带逐条引用的 Core 语义协议 to-spec，并在任务完成时锁定 Core 架构边界、责任归属、禁止依赖、单一事实权威规则和语义基线。

**WHY**：当前 `UniClaw.Kernel` 混合了跨领域语义、UI World realization、Effect Runtime 和 Harness。需要先建立可供浏览器、手机、机器人和仿真依赖的最小候选协议，再决定源码如何减法和迁移。

## Scope

- `docs/design/core-extraction-document-alignment-v0.1.md`：to-spec 前置文档归纳与引用基线。
- `docs/design/core-extraction-qspec-v0.1.md`：带来源、状态和阻塞范围的 Core 候选 to-spec。
- 以手机滚动页面—元素点击作为第一条 tracer bullet 的语义验收路径。
- 规定第二种非 UI realization 的反向验证门槛。
- 任务完成时锁定架构与语义基线；锁定不包含源码迁移、最终字段、继承树、包名或存储布局。

## Out of Scope（禁止）

- 不迁移或重命名 `src/UniClaw.Kernel`。
- 不创建完整生产 Core 项目、Runtime 状态机、设备驱动、识别算法或数据库协议。
- 不把独立 `Event`、完整 `WorldBeliefRevision`、Outcome、Verification、OpenDuties 或 Trace 冻结为最小 Core 事实。
- 不以当前类签名、sealed 关系或字段列表反推最终架构。

## Decisions

| # | 决策 | 状态 | 引用 |
|---|---|---|---|
| D1 | Core 顶层边界为 `Specification + World + Effect`。 | 已确认原则 | vNext.1 §3；对齐指南 §2；文档归纳 §4.1 |
| D2 | 当前基线保留六种核心语义记录：`Clause、Segment、Evidence、Claim、Event、Effect`；`Slice` 为 Segment 下条件性局部观察结构；`Attempt/TargetBinding` 为 Effect 内部职责。六种语义记录不等于六个独立类型；`Event` 的独立表示仍待验证。 | 同步既有结论 + 本轮澄清 | vNext.1 §5–§8；审阅附件；验证证据 §1、§3 |
| D3 | Core 可包含无外部 IO 的纯语义规则，但不包含设备、识别、持久化、Runtime 或 Harness 实现。 | 本轮补充规则 | Grill Q5；文档归纳 §4、§7 |
| D4 | 浏览器、机器人、仿真通过 realization/adapter 使用 Core；不要求旧类一一对应，也不预先冻结统一继承树。 | 同步既有结论 | vNext.1 §3、§10；对齐指南 §13；文档归纳 §4.9–§4.10 |
| D5 | 第一条 tracer bullet 为手机滚动页面—多 Slice—元素 Claim—点击 Effect/Attempt/Binding—点击后新 Evidence。 | 本轮补充规则 | 场景库 UI/滚动场景；验证证据 §3；Grill Q3、Q12 |
| D6 | to-spec 每条重要规则必须标明来源、状态、适用范围、禁止外推和缺保证时的阻塞范围。 | 本轮补充规则 | 用户明确要求；文档归纳 §1、§7 |
| D7 | 任务完成时锁定架构边界与语义基线；后续修改必须通过变更记录与反例说明。 | 本轮补充规则 | 用户明确要求；Grill Q18、Q19 |

## Acceptance

1. to-spec 明确 Core 责任、禁止依赖、单一事实权威和历史引用规则。
2. to-spec 中每条重要规则均可回指外部设计文档、对齐指南、审阅结论、仓库文档、场景范围或测试证据。
3. to-spec 明确 `Slice` 历史固定/可重建要求、`Effect → Attempt → TargetBinding` 分层和未知/冲突/过期/未授权的区分。
4. to-spec 明确旧模型允许删除、拆分、合并、替换、组合、引用和按语义继承，不要求一一映射。
5. to-spec 明确手机滚动—点击 tracer bullet、第二种非 UI realization 门槛和停止条件。
6. to-spec 明确 Event 独立类型、Slice 持久化、复杂因果和旧模型映射仍是验证缺口。
7. 任务收尾时，架构文档和语义基线完成锁定；源码迁移仍保持未执行状态。

## Constraints

- 外部材料冲突时以后续明确修订为准；源码只作现状证据。
- 任何尚未验证的建议必须标记为“本轮补充规则”“待验证”或“待裁决”。
- 缺少某项保证只阻塞依赖该保证的用途，不升级为全项目阻塞。
- 保留并行 dirty 文件，不进行 destructive git 操作，不提交 commit。

## Verification

```yaml
verification:
  level: CONTRACT
  method: 文档引用审查 + to-spec 状态/边界审查 + 与 core-extraction-document-alignment-v0.1.md 逐条对照
  expected: 每条重要规则有来源和状态；无源码迁移或未授权架构冻结声明
  actual: 通过。qspec 逐条覆盖 Acceptance 1–6；引用的 12 个仓库路径（2 源码、7 测试、evidence、3 架构/设计文档）全部存在；与对齐文档 §4–§7 逐条对照无矛盾；无源码迁移声明。Acceptance 7 已执行：qspec 状态头由 DRAFT / REVIEW_REQUIRED 升级为 LOCKED v0.1（锁定范围以 §10 与状态头 Lock Scope 为准）。
  evidence: docs/design/core-extraction-document-alignment-v0.1.md；docs/design/core-extraction-qspec-v0.1.md
```

## Status log

2026-09-18 · understanding→resolving · 完成 Grill 第 1–4 轮；确认先归纳文档再进入 to-spec，并确认 to-spec 必须逐条引用。
2026-09-18 · resolving→persisted · 创建文档归纳基线和 CORE-001 to-spec 任务；架构与语义基线尚未锁定。
2026-09-18 · persisted · to-spec 审查发现“六种核心记录”与“独立类型”表述混淆；已统一为六种核心语义记录，Event 独立表示继续待验证。
2026-09-18 · persisted→review/verify · Entry 复验通过（base=working-tree 与 git 实况一致）；REVIEW：qspec 对照 Acceptance 1–6 与对齐文档无缺陷；VERIFY（CONTRACT 级）：引用路径核查 12/12 存在。
2026-09-18 · verify→closed · 执行 Acceptance 7 锁定：core-extraction-qspec-v0.1.md 升级为 LOCKED v0.1 — CORE SEMANTIC BASELINE；源码迁移保持未执行；后续修订经变更记录与反例说明（D7）。
2026-09-19 · closed · 按 to-spec 技能模板补发布 `changes/CORE-001/spec.md`；不改变已锁定的 Core 语义基线，仅补齐交接所需的 Problem/Solution/User Stories/Implementation/Testing/Out of Scope 结构。
2026-09-19 · closed（验证回补）· CORE-002 会话全量回归发现 DocsMetadataTests 违规两处，均属本 Change 文档收尾缺陷：①qspec 锁定时 Authority 被改为非 NONE（收尾编辑引入的回归）；②alignment 文档缺 Authority 头（早前欠账）。已修复：qspec Authority 复位 `NONE（锁定由 Governing Change 记录承载，锁定≠冻结）`；alignment 补 `Authority: NONE`。修复后全 solution 514/514 绿。教训留痕：CONTRACT 级文档审查未跑 docs 分类学执法测试，验证方法学应包含该测试。
