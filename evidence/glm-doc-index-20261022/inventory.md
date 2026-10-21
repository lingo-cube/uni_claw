# UniClaw 文档索引盘点记录（docs/analysis + docs/design）

> Date: 2026-10-22
> Executor: GLM doc-index worker（DSH 会话）
> Base: bce0ec3a（工作树含并行未提交变更，见 §5）
> Scope: 仅 `docs/analysis/README.md`、`docs/design/README.md`、本 evidence 目录。未触碰任何正文、状态头、ADR、基线、Change 状态。

## 1. 方法

- `glob` 枚举 `docs/analysis/`、`docs/design/` 全部 `*.md` 与 `*.html`（排除 README.md 本身）。
- 与各 README 索引表逐行比对（第一列路径）。
- **状态一律取文档头部 `> Status:` 行原文**（docs/README.md §5"可由头部机器提取"）；不以文件日期推测权威。
- 用途一句话取自各文档标题 + 引言首段，逐一打开读头部（10-14 行）核实，不凭文件名猜测。

## 2. 整理前后覆盖数

| 目录 | 正文文件 | 整理前已索引 | 整理后已索引 |
|---|---|---|---|
| `docs/analysis/` | 36 个 .md | 22（61.1%） | 36（100%） |
| `docs/design/` | 33 个 .md + 3 个 .html | 23 md + 2 html | 33 md + 3 html（100%） |

design 索引另有 2 行外部交叉引用（`../../plans/…`、`../capability-hub/…`），不计入目录内覆盖数。

## 3. 补齐的缺失索引（25 项新增行）

### docs/analysis/（14 项；状态 = 头部原文）

| 文件 | 头部 Status |
|---|---|
| invariant-enforcement-matrix.md | LIVING / DERIVED |
| dsh-integration-research.md | RESEARCH_DELIVERED |
| product-context-and-prompt-management.md | DRAFT（设计梳理，未授权实现） |
| ui-automation-knowledge-and-memory-design.md | DRAFT（设计梳理/调研，未授权实现） |
| dsh-memory-research.md | RESEARCH_DELIVERED（调研产物，未授权实现） |
| uniagent-profile-industry-research.md | DRAFT（梳理/调研产物，未授权实现） |
| ui-automation-agent-gap-assessment-2026-10-08.md | DRAFT（研究输入，未授权实现） |
| android-automotive-ui-agent-gap-assessment-2026-10-09.md | DRAFT（研究输入，未授权实现） |
| uniclaw-field-flow-optimization-proposals-2026-10-14.md | REVIEWED / GRILL-ACCEPTED |
| settings-full-traversal-decision-maker-2026-10-15.md | ENVIRONMENT_CHARACTERIZATION / BOUNDED_STOP |
| settings-full-traversal-decision-maker-2026-10-15-v2.md | VERIFIED_BOUNDED_BLOCKED |
| agt019-followup-optimization-and-research-2026-10-17.md | DRAFT / CANDIDATE |
| ui-automation-test-function-reuse-inventory-2026-10-20.md | COMPLETE / READ_ONLY_INVENTORY |
| world-observation-recovery-grill-2026-10-21.md | DESIGN-DECISIONS-ACCEPTED / PERSISTED |

### docs/design/（10 md + 1 html；状态 = 头部原文）

| 文件 | 头部 Status |
|---|---|
| uniflow-gate-recalibration-proposal-v0.1.md | SUPERSEDED |
| uniflow-gate-recalibration-proposal-v0.2.md | DRAFT |
| capability-coverage-derivation-v0.1.md | DRAFT |
| scenario-reasoning-v0.1.md | DRAFT / CANDIDATE |
| decision-granularity-scenarios-v0.1.md | DRAFT |
| consultation-protocol-v0.1.md | DRAFT / CANDIDATE |
| componentization-map-v0.1.md | DRAFT（活地图：每次 change 关闭时更新） |
| runtime-capability-integration-seams-v0.1.md | DRAFT（头部另有 Grill: ACCEPTED · 2026-10-04） |
| agent-exception-escalation-and-observation-v0.1.md | DRAFT / CANDIDATE（文首另有 SUPERSEDED 存档声明） |
| agent-exception-escalation-and-observation-v0.2.md | GRILLED / DECISION-COMPLETE（2026-10-17） |
| state-machine-viz-v0.1.html | （HTML 无状态头；按本目录既有 html 行先例标 DRAFT / INTERACTIVE-VIZ, Authority: NONE） |

## 4. 修正的既有行（3 处，均保留原行只改错值）

1. analysis 索引 `uniclaw-architecture-protocol-overall-review-2026-10-11.md` 状态
   `REVIEW_IN_PROGRESS / ARCHITECTURE_AND_PROTOCOL_DIRECTION_ACCEPTED` →
   `COMPLETE / DELIVERED-BY-ARCH-ALIGN-001`（与该文档头部 line 3 一致；原文档非 M 状态，头部为已提交事实）。
2. design 索引 `plans/2026-09-19-core-010-reliable-execution-record.md` 路径失效
   （`docs/design/plans/` 不存在）→ `../../plans/2026-09-19-core-010-reliable-execution-record.md`
   （文件实际在仓库根 `plans/`，已核实存在）。
3. design 索引 `../capability-hub/customization-integration-development-protocol-v0.1.md` 状态过期
   `FROZEN / PROTOCOL GUIDE v0.1` → `FROZEN / PROTOCOL GUIDE v0.2（JEV-SEMANTIC-CAP-001 兼容执行扩展；CAP-010 升格）`
   （该文档头部当前值；注意该文档同时处于他人未提交修改中，见 §5）。

## 5. 并行执行者内容（全部保留，未改动）

- `docs/analysis/README.md` 在本次开始前已有他人未提交新增 7 行：
  module-boundary / directory-organization（2026-10-21 两篇）、component-engineering-closeout（2026-10-22）、
  lifecycle-completeness（2026-10-12）、completeness-check（2026-10-14）、mathematical-axioms（2026-10-14）。
  含表格中部一个空行分隔（两段表），按原样保留。
- 工作树另有大量他人未提交修改/未跟踪文件（docs/research/、evidence/agnes-*、jev-* 等），未触碰。
- 本次编辑期间发生一次并发写冲突（edit 工具拒绝后重读重试），最终状态以重读后合并为准。

## 6. 不索引项（带理由）

- `docs/design/protocol-workflow-steps.generated.js`：生成物/数据文件，非文档正文，不在索引范围。
