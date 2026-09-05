# Harness V2 — Phase 2 Skill Migration Matrix

> DocumentType: `HARNESS_MIGRATION_ANALYSIS`
> Status: `DRAFT / PHASE-2-PROPOSAL`（处置为建议，待 Phase 3 契约冻结后生效）
> Authority: `NONE`（本文件不建立架构权威、不授权删除、不改变现行 Harness）
> Scope: `.ai/skills/` 全部 18 个自有 Skill 的 V2 迁移处置建议；不含任何执行动作。
> 调查基线: `uni-agent` 谱系（`0771d7d` + `47aab75`）；调查日期 2026-09-05。
> 禁止边界: 不得依据本文件直接删除/重写 Skill；大规模变更须待 Phase 3 契约冻结与 Human Gate。

## 1. 输入证据

- Canonical Skill 源：`.ai/skills/`（18 个目录 + README.md；唯一正文源）。
- 适配层：`.agents/skills/`、`.dsh/skills/` 各 18 个相对符号链接（`../../.ai/skills/<name>`），
  `diff -rq` 零发散；同步机制 = `scripts/setup-dsh-skills.sh`（幂等重建，fail-closed）
  + `scripts/check-consistency.sh` C13（pre-commit 守护）。
- 消费路径：Codex 经 `.codex/agents/*.toml` required_skills → `tools/agent_profile_validator.py`
  （L17 `SKILL_SOURCE_DIRS = (.ai/skills,)`）；DSH 经符号链接目录扫描 + frontmatter。
- 结构事实：仅 `evidence-driven-debugging` 有 `references/`（10 文件 + fixtures/5 packet JSON + 2 schema）；
  其余 17 个均为单 SKILL.md。`openspec-*` 4 件套为外部工具生成（generatedBy 1.3.1），
  格式与手工 Skill 发散；`uniagent-evolution-loop` 缺 metadata 块。
- 工作流约束：`.ai/workflows/uniflow-coding-workflow.md` §7 Required Skill 选择表；
  `development-protocol.md` §16-17（Skill `Authority: NONE`，不进入约束优先级）。

## 2. 处置类别定义

| 类别 | 语义 |
|---|---|
| KEEP | 语义整体保留，V2 中继续存在（允许非破坏性改写引用源） |
| MODIFY | 保留主体，改写与 V2 契约冲突的部分（如 OpenSpec 引用） |
| MERGE | 语义拆分并入一个或多个 V2 目标（canonical generic skill / UniFlow 阶段 / 域 Skill），原入口移除 |
| DELETE | 生命周期 ceremony 类语义随 OpenSpec 弃用，无独立保留价值 |
| ARCHIVE | 移入 legacy 记录区，不再进入默认发现层，保留历史可检索 |

原则（directive §19）：通用工程方法优先交给成熟 Generic Skills；自有 Skill 只保留
UniFlow 特有能力与 UniClaw Domain 特有能力；不允许仅复制改名。

## 3. 迁移矩阵总表

| # | V1 Skill（行数） | V1 职责摘要 | 处置 | V2 去向 | 保留语义 | 移除语义 |
|---|---|---|---|---|---|---|
| 1 | openspec-propose (147) | 一键生成 proposal/design/specs/tasks | DELETE | 决策纪律 → UniFlow Decision/Plan 阶段 + Decisions 工件 | proposal/design 的决策前置纪律；acceptance 先行 | OpenSpec change 创建 ceremony；spec 目录即权威的假定 |
| 2 | openspec-apply-change (221) | 按 tasks.md 逐项实施并勾选 | DELETE | UniFlow Execute + `tdd` | 逐任务推进 + 即时完成标记；验证节奏（per-task gate） | OpenSpec tasks 勾选机制；change 目录绑定 |
| 3 | openspec-archive-change (158) | 毕业：提取 decisions、归档 | DELETE | 一次性迁移程序（Phase 6 legacy 降级用脚本/程序，非常驻 Skill） | 从历史 change 提取 Decisions/Contracts/Acceptance 的方法 | 常驻归档 ceremony（V2 无 OpenSpec 生命周期） |
| 4 | openspec-explore (289) | 思考伙伴式 explore mode | MERGE | UniFlow Explore 阶段指导 + `grilling` | 探索式提问、澄清需求、问题拆解 | explore → OpenSpec change 的输出绑定 |
| 5 | task-classification (90) | 任务类型路由（架构/协议/实现/Bug/文档/研究） | MERGE | UniFlow Route（WHEN/WHAT NEXT）；L0-L4 证据分级 → `diagnosing-bugs` 上下文 | 最小上下文选择纪律；分类先于行动 | 独立分类入口（分类是 UniFlow 路由职责） |
| 6 | architecture-context-loading (20) | L0-L5 分层按需加载 | MERGE | V2 Context Protocol（WorkItem 预算 + anchors/contract_refs 按需加载） | authority-first 加载顺序；历史默认不加载 | OpenSpec active artifacts 作为 L4 默认项 |
| 7 | architecture-change-safety-check (17) | 变更前 authority/lifecycle/回滚风险检查 | MERGE | `code-review`（pre-change 检查清单）+ UniFlow Decision gate | 权威/生命周期/回滚三风险轴 | 独立入口（并入 review 能力） |
| 8 | architecture-stop-condition (20) | 无权威/证据时安全停止并请求决策 | MERGE | UniFlow Stop/Escalation 语义（Human Gate 政策） | `ARCHITECTURE_DECISION_REQUIRED` 停止纪律 | 独立入口 |
| 9 | architecture-evidence-first-debugging (19) | 假设前的证据优先诊断 | MERGE | `diagnosing-bugs` | fixture/test/runtime/architecture 四因分离；证据先于假设 | 独立入口（与 #10 合流） |
| 10 | evidence-driven-debugging (302+refs) | E0-E4 证据分级 + Expected→Observed→Gap→FDP 工作流 | MERGE | `diagnosing-bugs`（通用主入口）+ UniClaw 域扩展 | E0-E4 证据等级；FDP/Owner/Root Cause；回归测试前置；references/ 结构范式 | 平行 debugging 入口地位；Worker 流中的 OpenSpec 引用 |
| 11 | runtime-behavior-debugging (116) | Runtime/FSM/Traversal/真机/flaky 失败分类 | MODIFY | `diagnosing-bugs` 的 UniClaw Runtime 域扩展（或 `runtime-engineering` 子节） | 失败分类学（Discovery/Grounding/Authorization/Execution/Recovery/Environment）；真机/非确定性处理 | 与 #10 平级的主入口地位（§16 单入口规则） |
| 12 | knowledge-health-check (106) | 知识结构只读审计 | MODIFY | 保留；审计对象从 decision-registry/OpenSpec 改为 V2 四类工件（Decisions/Plans/WorkItems/Evidence） | 漂移检测、冻结期审计、只读纪律 | OpenSpec 作为审计对象的引用 |
| 13 | knowledge-maintenance (73) | 现态/过程/历史三层知识维护 | KEEP | 保留（V2 工件区维护方法） | 三层分离；不改事实的治理原则 | — |
| 14 | decision-retrieval (50) | 按 ID 检索历史决策 | MODIFY | 保留；检索源增加 legacy OpenSpec records（降级后仍是历史证据层） | evidence-led 范围检索；历史≠现架构 | — |
| 15 | documentation-migration-safety (19) | 文档组织不改变历史事实 | KEEP | 保留（本次迁移本身依赖） | 事实/决策/gate 结论不可变性 | — |
| 16 | project-continuation (34) | 从投影恢复长程项目 | MODIFY | 保留；投影源切换为 V2 工件（snapshots/current-gates 再生成机制需 Phase 3 决定） | 现态恢复 + 历史按需 | OpenSpec active 作为恢复源的引用 |
| 17 | perception-model-intelligence (201) | 感知/ML 平台机器真理解释 | KEEP | 保留（UniClaw Domain 特有，只读推导层） | 生产模型/Candidate/TrainingRun 解读；零发布权威 | — |
| 18 | uniagent-evolution-loop (27) | 模拟/证据/FDP/Owner 路由受控演进 | MODIFY | 保留为 Domain loop；补 metadata 块；边界声明不得拥有 Dev Workflow（§21） | 演进循环的域语义 | （若含）创建 change/派发 worker 等流程语义 → UniFlow |

## 4. 重点说明

### 4.1 Debugging 收敛（directive §16 — 第一优先级重构）

现状是**三个平级入口**：`evidence-driven-debugging`（通用）、`runtime-behavior-debugging`
（域）、`architecture-evidence-first-debugging`（架构场景）。V2 目标单一主入口：

```text
diagnosing-bugs（canonical generic）
├── 吸收 Matt: Feedback Loop → Reproduce → Minimise → Falsifiable Hypotheses
│   → Instrumentation → Regression Test → Minimal Fix
├── 吸收 evidence-driven-debugging: E0-E4 / Expected vs Actual / FDP / Owner
└── UniClaw Runtime 域扩展（源: runtime-behavior-debugging 失败分类学）
```

强制规则照抄 directive §16（No reliable RED → No fix；No FDP/Owner → No
Implementation WorkItem；…）。`E0-E4` 与失败分类学是本仓库独有、必须保留的语义。

### 4.2 OpenSpec 四件套（directive §2）

`openspec-propose/apply/archive/explore` 的生命周期 ceremony 随 OpenSpec 弃用。
唯一需要延续的是**一次性 legacy 降级程序**（13 active + 81 archived + 78 个
`openspec/specs/` 主干中提取 Decisions/Contracts/Invariants/Acceptance/Compatibility），
它是 Phase 6 的迁移脚本/程序，不是常驻 Skill。`openspec-explore` 的思考伙伴语义
是四件套中唯一进入 V2 常驻层的部分（→ UniFlow Explore + `grilling`）。

### 4.3 directive §20 目标域 Skill 现状核对（repo 真相）

directive 要求"重点保留或重构"的 5 个域 Skill **当前均不存在**于 `.ai/skills/`：

| 目标 Skill | 现状语义来源（V2 创建时吸收） |
|---|---|
| trace-collection | `development-protocol.md` §17.2 步骤2；evidence-driven-debugging 的 trace/observation 采集；openspec change 内 evidence/ 目录惯例 |
| scenario-testing | `development-protocol.md` §3 Scenario-First + §17.4；`tests/UniClaw.Runtime.Tests/AGENTS.md`；L1-L4 资产分级 |
| runtime-engineering | 宪章 60 节 + Contract I-1..I-14 的工程化指导；runtime-behavior-debugging 的边界知识 |
| runtime-scenario | Scenario 目录与 tests/Scenario/ 的场景设计方法 |
| state-machine-integration | FSM 相关测试协议（I-7 状态机只做 protocol transition） |

处置：**Phase 5 创建**（自有 Skill 迁移阶段），不在本矩阵内虚构行；trace 可视化
统一交给 `show-me`（trace-collection 产出结构化 trace → show-me 呈现）。

### 4.4 V2 新增 Generic Skills（directive §13 allowlist，Phase 4 安装）

```text
grilling · domain-modeling · codebase-design · tdd · code-review
· diagnosing-bugs · show-me
```

进入统一 canonical 层（`skills/<name>/SKILL.md` 格式），不控制 UniFlow、不建
第二套 task system、不决定 Model Routing。**明确不引入**（§14）：ask-matt、
to-spec、to-tickets、implement、triage、wayfinder（吸收 tracer bullet /
self-contained unit / DAG 思想进 ToWorkItems 即止）。

## 5. 与 V2 验收对应

| directive 验收条目 | 本矩阵贡献 |
|---|---|
| 只保留一个 debugging 主入口 | §4.1：三入口 → `diagnosing-bugs` + 域扩展 |
| 只保留一个 review 主入口 | #7 并入 `code-review`；`tdd`/`code-review` 为统一能力 |
| 没有 OpenSpec 依赖 | #1-#4 的 DELETE/MERGE 处置 + 一次性降级程序 |
| 一套 Skill source | 维持 `.ai/skills` canonical + 符号链接 adapter（零发散已验证） |
| Skill 不选模型 | 全部 Skill 无模型绑定；绑定只在 model-routing.yaml |

## 6. 开放问题（供 Phase 3 契约冻结裁决）

1. `runtime-behavior-debugging` 域扩展放 `diagnosing-bugs` 内还是 `runtime-engineering`
   内（影响 §4.1 结构）。
2. 一次性 legacy 降级程序的形态：脚本（进 scripts/）还是临时 Skill（用后 DELETE）。
3. `uniagent-evolution-loop` 与 UniFlow 的边界声明落点（SKILL.md 内 vs UniFlow 文档内）。
4. modules.json 的 `perception-platform`（数据有、workflow 文档无）在 V2 ModuleProfile
   中的去留 —— 与本矩阵 #17 相关。
5. Skill canonical 目录命名：directive §4 示意 `skills/<name>/`，现仓为 `.ai/skills/` —
   Phase 3 决定是否迁移路径（本矩阵按现路径表述）。

—— 调查证据来源：subagent 只读盘点（.ai/.agents/.dsh 三目录 diff、setup 脚本、
validator 源码）+ 第一手阅读（uniflow-coding-workflow.md、development-protocol.md、
task/result 契约提取、change-classification.md）。本文件为 Phase 2 交付物，未执行任何删除。

## 7. 对齐修正附录（2026-09-05，上游安装实录）

Alignment Directive 下达后的实际执行（证据：`skills-lock.json`、
`harness-v2-skill-provenance-inventory.md`）：

1. **删除**：此前按本矩阵「V2 目标结构」自撰安装于 `skills/` 根目录的 7 个
   SKILL.md——属 directive 禁止的「聊天重写版本」，矩阵原建议中「自撰 canonical
   版本」的路径作废。
2. **安装**：从官方上游原文安装 9 个——mattpocock/skills 8 个
   （setup-matt-pocock-skills / grilling / grill-with-docs / domain-modeling /
   codebase-design / tdd / code-review / diagnosing-bugs）+ humanlayer/skills
   的 show-me；落点 `.agents/skills/`（installer 标准输出）。
3. **本地语义保留方式变更**：UniClaw 特有调试语义（E0-E4 / FDP / Owner /
   失败分类学）不再并入上游正文（§7 默认不改上游），改为 Phase 5 以
   LOCAL_UNICLAW 扩展 / reference 组合承载；§4.1 的「吸收」语义相应读作
   「组合使用」而非「改写上游」。
4. 本矩阵 §4.4 的 7-skill allowlist 由 9-skill 上游 allowlist 取代
   （新增 grill-with-docs、setup-matt-pocock-skills）。

## 8. Directive 规定列补全（2026-09-06，V2 Migration Directive §16）

按新 directive 要求的八列格式补全（对象 = repo 真实存在的 18 个 V1 Skill，
路径与来源经 Phase 0 实证；「Source」= LOCAL_UNIFLOW / LOCAL_UNICLAW / 外部生成）：

| Old Skill | Source | Current Path (V1) | Unique Semantics | Upstream Overlap | 处置 | V2 Destination |
|---|---|---|---|---|---|---|
| evidence-driven-debugging | LOCAL_UNIFLOW | .ai/skills/ | E0-E4 证据分级；Expected→Observed→Gap→FDP；RED→GREEN 回归纪律 | diagnosing-bugs（feedback loop / reproduce / minimise / hypotheses） | MERGE | 本地调试扩展（composition，不改上游） |
| runtime-behavior-debugging | LOCAL_UNICLAW | .ai/skills/ | 失败分类学（Discovery/Grounding/Authorization/Execution/Recovery/Environment）；真机/flaky | diagnosing-bugs | MERGE | 同上（域附录） |
| architecture-evidence-first-debugging | LOCAL_UNIFLOW | .ai/skills/ | fixture/test/runtime/architecture 四因分离 | diagnosing-bugs | MERGE | 同上 |
| task-classification | LOCAL_UNIFLOW | .ai/skills/ | 最小上下文选择纪律 | UniFlow Route + grilling | MERGE | uniflow.md Route/启动条件 |
| architecture-context-loading | LOCAL_UNIFLOW | .ai/skills/ | authority-first L0-L5 加载顺序 | CONTEXT.md/ADR 约定 + WorkItem 预算 | MERGE | uniflow.md §7 + anchors/contract_refs |
| architecture-change-safety-check | LOCAL_UNIFLOW | .ai/skills/ | authority/lifecycle/回滚三风险轴 | code-review | MERGE | code-review 组合清单 |
| architecture-stop-condition | LOCAL_UNIFLOW | .ai/skills/ | 无权威即停（ARCHITECTURE_DECISION_REQUIRED） | UniFlow Human Gate / STAY_IN_EXPLORE | MERGE | uniflow.md §2/§13 |
| openspec-propose / apply / archive | 外部生成(1.3.1) | .ai/skills/ | （生命周期 ceremony，随 OpenSpec 退出） | — | DELETE | 一次性 legacy 降级程序（非常驻） |
| openspec-explore | 外部生成(1.3.1) | .ai/skills/ | 思考伙伴式探索 | grilling / grill-with-docs | MERGE | Pre-UniFlow Explore（上游 skill 承担） |
| knowledge-health-check / knowledge-maintenance | LOCAL_UNIFLOW | .ai/skills/ | 知识三层治理与漂移审计（对象改为 V2 工件） | 无 | KEEP/MODIFY | Phase 15 本地 skill |
| decision-retrieval / project-continuation / documentation-migration-safety | LOCAL_UNIFLOW | .ai/skills/ | 历史检索 / 现态恢复 / 文档迁移安全 | 无 | MODIFY | Phase 15 本地 skill |
| perception-model-intelligence | LOCAL_UNICLAW | .ai/skills/ | 感知/ML 平台解读（只读推导层） | 无 | KEEP | Phase 15 本地域 skill |
| uniagent-evolution-loop | LOCAL_UNICLAW | .ai/skills/ | 模拟/证据/FDP/Owner 受控演进 | 无 | MODIFY | Phase 15（边界：不拥有 workflow） |

**directive 审计清单中不存在于 repo 的项**（Phase 0 实证为「无此文件」，
不虚构行）：`trace-collection`、`runtime-development`、
`runtime-scenario-development`、`state-machine-integration`、
`integrated-test-gen`、`test-scenario-generation-evaluation`、`skill-routing`。
其中仍有价值的语义（trace 采集、场景测试设计、FSM 集成约束）按 Phase 15
以本地扩展按需创建，来源标注 LOCAL_UNICLAW。

本分支（uni-harness）当前无 OpenSpec、无重复 Debug/Review 入口、无第二
task surface——§26 步骤 16-17 在本分支天然满足；uni-agent 谱系的
OpenSpec 降级与重复入口删除属该分支的后续工作。
