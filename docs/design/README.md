# design/ 索引（未裁决方案 · Authority: NONE）

> 规则见 docs/README.md §1/§2/§3：草案裁决接受后必须迁入对应权威目录。
> Status: DRAFT（梳理/调研产物，未授权实现）
> Authority: `NONE`（分析产物，不是架构决策；与 ADR / baseline 冲突时以后者为准）

| 文档 | 用途 | 状态 |
|---|---|---|
| `product-architecture-migration-baseline.md` | 产品架构迁移方案（历史候选） | CANDIDATE / NOT_ADOPTED |
| `legacy-to-target-architecture-mapping.md` | legacy → Target 架构映射方案 | CANDIDATE / NOT_ADOPTED |
| `harness-v2-phase2-skill-migration-matrix.md` | V2 skill 迁移矩阵（Phase-2 提案） | DRAFT / PHASE-2-PROPOSAL |
| `uniagent-dual-realization-architecture-v0.1.md` | UniAgent 双 realization 的 UAR-001 设计与评审历史 | CANDIDATE / SUPERSEDED_BY_FROZEN_BASELINE |
| `runtime-flow-simulation-core-model-v0.1.md` | Runtime 闭环、异步 Perception、页面还原、资产回放与 Simulation 核心模型/接口 | DRAFT / REVIEW_REQUIRED |
| `core-extraction-qspec-v0.1.md` | Core 提取 to-spec 首版：架构边界/责任归属/禁止依赖语义基线（字段表见 v0.2） | LOCKED v0.1 — CORE SEMANTIC BASELINE |
| `core-extraction-qspec-v0.2.md` | 基于 vNext.1 的最小候选 Core 字段审阅、UI realization 首版边界及 CORE-008 Attempt/Trace/Slice 修订 | CANDIDATE / REVIEWED；Review Lock: CORE-007，语义修订：CORE-008 |
| `core-extraction-document-alignment-v0.1.md` | Core 提取前夜的设计文档对齐 | PRE-SPEC / CANDIDATE |
| `core-model-responsibility-matrix-v0.1.md` | Core 模型责任矩阵（CORE-004 责任对齐） | CANDIDATE / REVIEWED |
| `core-extraction-uiworld-alignment-v0.1.md` | Core 契约 × UI World realization 只读对齐表：覆盖矩阵、缺口归属分类、文档漂移修正、Step 2-4 路线（CORE-015） | DRAFT / READ-ONLY ALIGNMENT |
| `uiworld-realization-contract-v0.1.md` | UI World realization 契约草案：输入/输出/不得拥有清单 + Core 条目→UI 义务映射（CORE-015 Step 2） | DRAFT / REVIEW_REQUIRED |
| `core-execution-record-contract-v0.1.md` | 可靠执行记录契约与验收计划（不含实现） | DESIGN / APPROVED；CORE-009 |
| `plans/2026-09-19-core-010-reliable-execution-record.md` | 首个可靠执行记录受控实现计划（CORE-010 规划实体；已执行：CORE-011 仿真、CORE-013 产品/ADR-0023） | PLAN / APPROVED；已执行 |
| `core-execution-record-simulation-contract-v0.1.md` | CORE-010 测试侧仿真、Crash Cut Point 与验收契约（不含实现） | DESIGN / APPROVED；CORE-010 |
| `runtime-core-model-interface-archive-skeleton-v0.1.md` | Runtime/Core 模型接口 archive 骨架 | DRAFT / REVIEW_REQUIRED |
| `runtime-perception-world-simulation-replay-roadmap-v0.1.md` | Runtime 感知/世界/仿真/回放路线图 | DRAFT / ACCEPTED_AS_CHANGE_INPUT（H2） |
| `uniclaw-workspace-architecture-v0.1.md` | UniClaw Workspace 架构方案 v0.1（目标/边界/分层/数据形状/前端组织；PNL-003 主文档，AGT-009 基线卫生修复迁入） | DRAFT / REVIEW_REQUIRED |
| `../capability-hub/customization-integration-development-protocol-v0.1.md` | Capability 定制化、协议选择、注册集成、Adapter 开发与验收流程（Capability Hub 管理入口） | FROZEN / PROTOCOL GUIDE v0.1（CAP-010 升格） |
| `memory-system-component-declaration-v0.1.md` | Memory System 组件声明占位（给 UniAgent 提供记忆；零落地待 buyer；AGT-018） | CANDIDATE / DECLARATION |
| `task-scoped-capability-binding-v0.1.md` | 任务级能力创建、绑定、隔离、取消与收尾；生产接口待实现 | DRAFT / ACCEPTED_AS_CHANGE_INPUT |
| `uniagent-profile-realization-and-management-v0.1.md` | UniAgent Profile 落地设计：产品级 host-neutral profile + DSH 绑定分离（四层装配、17 项裁决闭合、切片 PRF-1a/1b/2/3/4/5、ADR-0041 计划） | DRAFT / GRILLING_DECIDED（待 change 执行） |
| `uniclaw-architecture-overview-v0.1.html` | 架构总览信息图（培训/产品介绍用；受众分层借鉴 C4 模型：上下文→五大件→六域→流程→生命周期→优势→上电地图） | DRAFT / PRESENTATION（Authority: NONE，与基线冲突以基线为准） |
| `uniclaw-architecture-overview-presentation-v0.1.md` | 架构总览讲演底稿（面向业界开发者与潜在客户：定位/失败模式/三公理/架构/点击旅程/生命周期/工程可信度/QA 底稿） | DRAFT / PRESENTATION-DRAFT（Authority: NONE，与基线冲突以基线为准） |
| `component-interface-and-data-protocol-lockdown-plan-v0.1.md` | 组件接口与数据协议锁定计划：三层锁定含义（语义/形状/字节）、接口盘点（六域/组合缝/驱动面/wire）、快照执法机制、四阶段执行与承重审阅清单 | DRAFT / PROPOSAL（待 Owner 裁决四点后立 change） |
| `protocol-completeness-formal-audit-v0.1.md` | 协议完备性形式审计：生命周期/流程全走查，Safety/Liveness/Reachability/Consistency 数学检查 + 认识论/政体哲学审计；九项发现 + 附录A 字段级落实清单（缺/多字段逐接口）+ 附录B 流程字段链二次走查 + 附录C grill 裁决记录（11 题全裁决） | DRAFT / FORMAL-AUDIT（裁决已固化 ADR-0048/0049/0050；实施待 PROTOCOL-FIX） |
| `protocol-fix-001-proposal.md` | PROTOCOL-FIX 修复包提案：六切片实施计划（Origin 字段族/期望载荷/预算两层顶+续授/locator 收窄/渗透立法/机械搭车），含验收与锁定计划衔接 | DRAFT / PROPOSAL（待 Owner 批准开工，立 changes/PROTOCOL-FIX-001） |
| `protocol-workflow-model-v0.1.html` | 交互式协议工作流模型：27 步全链（任务→契约→运行→交卷），点击节点查看该步字段传递与修复标记（＋新增/✖移除/chg变更），可按修复点/不动/轨道过滤 | DRAFT / INTERACTIVE-MODEL（Authority: NONE；字段为读码实测） |
