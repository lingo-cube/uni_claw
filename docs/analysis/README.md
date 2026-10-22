# analysis/ 索引（考察既有现实 · Authority: NONE · 永非规范）

> 规则见 docs/README.md §1/§2/§3：分析结论要产生约束力，必须经 change
> Status: DRAFT（梳理/调研产物，未授权实现）
> Authority: `NONE`（分析产物，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> 落入 design/adr/architecture，而非原地升格。

| 文档 | 用途 | 状态 |
|---|---|---|
| `harness-v2-compatibility-matrix.md` | V2 兼容性矩阵 | COMPLETE / EVIDENCE-BACKED |
| `harness-v2-phase0-inventory.md` | V2 Phase-0 盘点 | COMPLETE / EVIDENCE-BACKED |
| `harness-v2-phase1-protocol-inventory.md` | V2 协议盘点 | COMPLETE / EVIDENCE-BACKED |
| `harness-v2-semantic-migration-matrix.md` | V2 语义迁移矩阵 | COMPLETE |
| `harness-v2-skill-provenance-inventory.md` | skill 溯源盘点（真相在 skills-lock.json） | COMPLETE |
| `legacy-perception-evidence-assessment.md` | legacy 感知资产评估（PER-002 证据基础） | CANDIDATE / NOT_ADOPTED |
| `uni-agent-perception-implementation-analysis.md` | legacy 感知实现分析（PER-005 证据基础） | CANDIDATE / NOT_ADOPTED |
| `uni-agent-trace-replayability-analysis.md` | legacy Trace 可重放性分析 | CANDIDATE / NOT_ADOPTED |
| `uniclaw-product-architecture-migration-baseline-l0-l3.md` | 基线拆分导航（→ architecture/ 三资产） | SPLIT / ROUTER |
| `per-005-algorithm-library-options.md` | OPT 系列候选调研（算法/库/优化） | CANDIDATE / NOT_ADOPTED |
| `per-005-ocr-backend-research.md` | OCR/推理后端候选调研 | CANDIDATE / NOT_ADOPTED |
| `dsh-tool-scope-restricted-sessions.md` | DSH 工具可见性语义与受限会话正确实现形状 | COMPLETE / EVIDENCE-BACKED |
| `agt-002-b1-fix-instruction.md` | AGT-002 B1 工具面泄漏修复指令（据上篇起草） | CANDIDATE |
| `fast-perception-strategy-seam-fate.md` | IFastPerceptionStrategy 去留裁决材料：牵连面盘点 + 三路线成本（PER-021） | COMPLETE / EVIDENCE-BACKED |
| `sim-006-first-baseline-matrix.md` | SIM-006 首批 8 场景+组件契约双轴基线矩阵（A1 交付物） | COMPLETE / EVIDENCE-BACKED |
| `uniclaw-architecture-protocol-overall-review-2026-10-11.md` | UniClaw 产品架构、层级轴、概念、Owner/Authority、P1–P26 与外围 O1–O12 协议审阅底稿 | COMPLETE / DELIVERED-BY-ARCH-ALIGN-001 |
| `uniclaw-module-boundary-design-review-2026-10-21.md` | 模块职责与边界审查：逐组件检查、3 个 P1 与 4 个 P2、隔离复验证据 | COMPLETE / READ_ONLY_REVIEW |
| `uniclaw-directory-organization-review-2026-10-21.md` | 组件目录组织审查：导航、物理归属、产物分离、索引和候选整理顺序 | COMPLETE / READ_ONLY_REVIEW |

| `uniclaw-component-engineering-closeout-2026-10-22.md` | ARCH-DOC-024独立组件分级、工程化P0/P1/P2清单及已实施/剩余范围 | COMPLETE / EVIDENCE-BOUNDED |
| `uniclaw-lifecycle-completeness-and-optimization-2026-10-12.md` | ARCH-DOC-025历史生命周期、150场景、数学/哲学审查；后续字段复核见下一项 | COMPLETE / SUPERSEDED-HISTORICAL |
| `uniclaw-completeness-check-2026-10-14.md` | 字段链与最小buyer历史复核，第六节修正身份/P24/P25旧阻塞 | COMPLETE / BUYER-SCOPED-HISTORICAL |
| `uniclaw-mathematical-axioms-philosophical-derivation-2026-10-14.md` | 字段公理和哲学推导历史前提，实例判定受同日buyer复核修正 | COMPLETE / HISTORICAL-PREMISES |
| `invariant-enforcement-matrix.md` | 不变量×执法覆盖矩阵：产品基线 §20+§24.10 的 47 条不变量与执法点映射（完整性由 tools/check-invariant-matrix.py 守护，ARCH-DOC-016） | LIVING / DERIVED |
| `dsh-integration-research.md` | DSH 运行时嵌入 uni_claw 研究（含安装侧/仓库侧两层 profile 定制；ADR-0022 embedding 主线） | RESEARCH_DELIVERED |
| `product-context-and-prompt-management.md` | 产品上下文与 Prompt 管理梳理：六类输入（指令/安全/契约/运行上下文/历史知识/实时 Evidence）的 owner、生命周期与可信度拆分 | DRAFT（设计梳理，未授权实现） |
| `ui-automation-knowledge-and-memory-design.md` | UI 自动化测试的知识与记忆系统设计分析（平台/语义/设备/任务/经验五类知识需求） | DRAFT（设计梳理/调研，未授权实现） |
| `dsh-memory-research.md` | DSH 默认 Memory 机制与按机型召回研究（基于 dk-harness 源码与官方文档的只读调研） | RESEARCH_DELIVERED（调研产物，未授权实现） |
| `uniagent-profile-industry-research.md` | UniAgent Profile 业界一手资料研究（职责边界/配置版本/模型/工具/记忆/安全策略） | DRAFT（梳理/调研产物，未授权实现） |
| `ui-automation-agent-gap-assessment-2026-10-08.md` | UniClaw 成为完备 UI 自动化测试专业 Agent 的 Gap 调研（只读审查产品代码、前端、测试资产与环境工具） | DRAFT（研究输入，未授权实现） |
| `android-automotive-ui-agent-gap-assessment-2026-10-09.md` | Android 手机/平板与车机（AAOS/Android Auto/head unit）UI 自动化 Agent Gap 调研 | DRAFT（研究输入，未授权实现） |
| `uniclaw-field-flow-optimization-proposals-2026-10-14.md` | 字段完备性与生命周期流程检查合并成的可裁决优化建议（grill 前候选方案集） | REVIEWED / GRILL-ACCEPTED |
| `settings-full-traversal-decision-maker-2026-10-15.md` | Settings 全遍历决策者测试报告：五轮环境/决策测试，证明一级导航稳定与安全拒绝有效，但未证明全树完成 | ENVIRONMENT_CHARACTERIZATION / BOUNDED_STOP |
| `settings-full-traversal-decision-maker-2026-10-15-v2.md` | Settings 全遍历决策者验证与前沿报告 v2：验证边界与阻塞结论（分析证据，非产品权威） | VERIFIED_BOUNDED_BLOCKED |
| `agt019-followup-optimization-and-research-2026-10-17.md` | AGT-019 Event Stream / Heartbeat / 事件触发咨询设计与裁决定稿（v3.4；RUN-006 真机弹窗死循环谱系；实现须经 change） | DRAFT / CANDIDATE |
| `ui-automation-test-function-reuse-inventory-2026-10-20.md` | Agnes+YOLO+Jev UI 自动化测试：详细功能清单与当前代码复用评估（关联 UI-TEST-REUSE-001） | COMPLETE / READ_ONLY_INVENTORY |
| `world-observation-recovery-grill-2026-10-21.md` | World 身份补证与遍历恢复讨论（Q1–Q4 已接受决定；正式范围与验收见 changes/WORLD-OBSERVATION-001） | DESIGN-DECISIONS-ACCEPTED / PERSISTED |
| `jev-ui-layout-semantic-recognition-research.md` | Jev 用于 UI 布局识别/语义识别的可行性调研（布局不可承担；语义只在已枚举候选上判别） | CANDIDATE / NOT_ADOPTED |
| `world-model-capability-gap-and-kernel-fallback-assessment-2026-10-22.md` | World 模型胜任性/当前-设计 Gap/Kernel 兜底健壮性评估（含设计缺陷 vs 实现缺陷归类） | COMPLETE / EVIDENCE-BOUNDED |
