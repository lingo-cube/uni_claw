# Phase 2 + Phase 2.5 Unified Closeout Audit — Result

DocumentType: `CLOSEOUT_AUDIT`
Decision: `PROJECT_LEADER_PHASE2_PHASE25_UNIFIED_CLOSEOUT_AUDIT_RESULT`
Date: 2026-08-26
Auditor: DSH coding agent (Sol role) — 全量重新枚举，未采信任何摘要数字
Authority: 只读审计 + 必要文档状态修正；零生产代码改动。

---

## 1. Executive Decision

**UNIFIED_ARCHIVE_READY_PENDING_HUMAN_GATE（附两项前置清理）**。全部 87 项工作树变更
（33 M + 54 untracked）完成归属分类，**零 I 类（unknown ownership）**；Phase 2 七文件确认为
**已毕业实现的未提交基线**（非 POST_GRADUATION_DRIFT，mtime + diff 内容双重证明）；
Phase 3 草案隔离干净。两项小清理（见 §13）完成后即可进入统一 Archive Human Gate。

## 2. Full Worktree Inventory（重新枚举，非复述）

`git status`：**33 modified + 54 untracked = 87 项**（此前摘要的"7 在途 + 约 54 新增"不准确：
modified 33 项中含 Phase-2 RESAR 的 12 项与治理/工具/投影文档 21 项）。

## 3. Ownership Classification

### A. Phase 2 capability implementation（12 项，全部 RESAR）
| 文件 | mtime | 归属 |
|---|---|---|
| src/UniClaw.Runtime/Planning/StrategyContract.cs | 0825_21:27 | RESAR（admission 派生 ExplorationExecutionSemantics，diff 实证 +8 处） |
| src/UniClaw.Runtime/Planning/IntentExecution.cs | 0825_21:27 | RESAR |
| src/UniClaw.Runtime/Agent/Agent.cs / Agent.OpenWorld.cs / Agent.PreTerminalCycle.cs | 0825_21:58–22:20 | RESAR（rule path / identity record） |
| src/UniClaw.Runtime/Model/ExplorationLedger.cs / ExplorationLedgerCompiler.cs | 0825_22:25–27 | RESAR（identity-exact accounting；diff 实证 ExplorationRuleResolver） |
| tests/…/Strategy/StrategyTestSupport.cs + Unit/Strategy/StrategyContractTests.cs | 0825_21:22 | RESAR 测试（maximumDepth 参数化，diff 实证） |
| tests/…/Unit/{ExplorationLedger,ExplorationDepthBoundary,UnresolvedNodeFailClosedPath}Tests.cs | 0825 | RESAR（reopened→remediated 证据链） |
| **src/UniClaw.Runtime.PhysicalHost/PhysicalHostComposition.cs + tests/…/Integration/ServerModeIntegrationTests.cs + openspec/changes/archive/2026-08-21-uniclaw-driverhost-production-server-mode/{README,tasks}.md** | 0826_20:39–21:10 | **A 类外延（见 §4 注）**：毕业后的 graduation repair —— 归属已归档 change `uniclaw-driverhost-production-server-mode` 的 graduation-repair 增量（注入式 RunGraphFactory 可测性 seam，diff 注释自证归属与"零行为变化"），其 tasks.md 增量勾选即证据。生产语义无变化（默认路径 byte-equal，仅新增可选参数）。 |

### B. Phase 2 graduation / remediation evidence（8 项 untracked + 3 项 modified）
- 决策：runtime-exploration-ledger-and-depth-control-graduation-**reverification**（REVOKED）、
  phase2-**capability-baseline-freeze**、phase2-**final-graduation-decision**（GRADUATED，
  Changes 字段同时指向 ledger 前驱与 RESAR 后继）、roadmap-phase2-consistency-analysis、
  project-leader-uniflow-phase2-baseline-freeze-…-result。
- modified：前驱 graduation-decision（头部已改 SUPERSEDED，历史保留）、前驱 tasks.md
  （revocation 勾选回退，diff 实证）、docs/decisions/index.md（新决策入册）。

### C/D. Phase 2.5 Harness 实现 + 测试（38 项）
src/UniClaw.Runtime.ValidationHarness/（29 源文件，8 子区）+ tests/…/ValidationHarness/
（9 测试文件）+ tests csproj（+ValidationHarness 引用，注释自证 WI-EVH-001）+
src/UniClaw.Runtime.sln（注册行）。

### E. Phase 2.5 runtime evidence（14 项 tierb-*）
全清单与引用图谱见 §6。

### F. Phase 2.5 decisions / lifecycle（7 项）
implementation-result、tierb-validation-result、goal-evaluation-alignment、
s2-semantic-remediation、tierb-s1-normalization-remediation、tierc-…-result、
graduation-review、graduation-decision + workitems/WI-{ERB,EVH}-00x（9 个 UniFlow 工件）+
current-gates / latest snapshot / roadmap（lifecycle 投影 modified）。

### G. Phase 3 draft（1 目录）
openspec/changes/uniagent-local-exploration-memory/（draft-only，0/0 tasks，strict PASS）。

### H. 治理 / 工具 / 投影（其余 modified + 2 untracked）
.ai/profiles/modules.json（runtime-integration owned-paths 扩展，validator PASS）、
.dsh/profile-adapter/{profile-source.yaml,README}（revision 钉扎 + CLI 文档）、
tools/dsh_profile_adapter.py（dispatch/receipt/integrity CLI）、
tests/AgentWorkflow/test_dsh_cli_dispatch_receipt.py、.dsh/…/state/{events.jsonl,dispatches/}、
tmpDecision/dsh-uniflow-agent-loop-design.md、.codex/config.toml（effort 配置）、
protocol-violation-records / workflow-failure-analysis-v2（状态修正）。

### I. Foreign / unknown
**零项。** 每一文件均落到上述八类之一，且至少有 mtime、diff 内容、WorkItem id 或
OpenSpec 引用之一的直接证据。

## 4. Phase 2 七文件审计（重点重做）

"七文件"实为 RESAR 生产侧 7 项（§3 A 表前 7 行）。逐文件结论：

| 问题 | 答案 |
|---|---|
| 属于哪个 change | `runtime-exploration-semantic-admission-remediation`（RESAR；WI-RESAR-001..008 在库对应） |
| 是否被 graduation evidence 覆盖 | 是——`runtime-exploration-phase2-final-graduation-decision`（GRADUATED，2026-08-25）显式同时覆盖前驱 + RESAR |
| 是否生产源码 | 是（5 Runtime 源 + 2 测试支撑） |
| 是否通过最终 regression | 是（本轮 fresh 重跑 2109/2109 + 32/32；graduation 决策 §7 记录同基线） |
| 是否存在未记录语义变化 | 否——RESAR proposal 的 Impact 节逐文件点名这 7 个路径 |
| 是否进入 graduation baseline | 是（final-graduation-decision 以工作树状态为验证基线） |
| 为何仍显示 diff | **HEAD=e2d8dd4 早于 RESAR 实现；毕业验证发生在未提交工作树上，尚未形成干净提交基线** |

**POST_GRADUATION_DRIFT 判定：未发生。** 证据：7 文件 mtime 全部 ≤0825_22:27，早于
Phase-2 final graduation（0825 深夜）与 Phase 2.5 全部工作（0826）；本轮 fresh regression
与毕业决策记录的数字一致（2109/2109）。**例外注记**：§3 A 表末三行
（PhysicalHostComposition + ServerModeIntegrationTests + 已归档 change 的 README/tasks）
mtime 为 0826 晚（毕业之后）——它们不是 Phase-2 七文件，而是**已归档 change
`uniclaw-driverhost-production-server-mode` 的 graduation repair**（注入式 RunGraphFactory
可测性 seam；默认路径零行为变化；其已归档 tasks.md 的增量勾选即授权记录）。严格说这是
"对已归档 change 的事后 graduation-repair 增量"，归属与语义都清楚、无漂移，但它意味着
**archive 目录内容发生了 post-archive 编辑**——列入 §13 清理项 2。

## 5. Phase 2.5 File Mapping（Requirement → File → Test → Evidence）

毕业评审 §3 的映射表经抽样复核仍然成立（符号逐一在库：Scenarios 3 类、
Emulator 5 文件、Results 5 文件、Reporting 3、Classification 2、Fixtures 6、Hosting/Wire 2、
TierBProgram；测试 9 文件 56 用例）。**生产侧新增只在 harness/tests/evidence/docs**；
Runtime 生产源零 Phase-2.5 ownership change（guards 61/61 持续断言零反向引用 + 冻结 SHA）。

## 6. Evidence Retention Plan

| 证据 | 分类 | 依据 |
|---|---|---|
| tierb-s1-8of8-{PASS.json,runlog,screen.png} | **KEEP_FOR_ARCHIVE** | 毕业声明第 5 条直接依赖 |
| tierb-s1-3of8-hardened-proof.json | **KEEP_FOR_ARCHIVE** | RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS 负向证明 |
| tierb-s2-{result,postS1remediation}.json + runlog（anomaly injected 行）+ screen.png | **KEEP_FOR_ARCHIVE** | S2 bounded fail-closed 声明 |
| tierb-s3-{result,postS1remediation}.json + runlog | **KEEP_FOR_ARCHIVE** | S3 插入点证明 |
| tierc-…-result.md | **KEEP_FOR_ARCHIVE** | Tier C WAIVED_BY_HUMAN 记录 |
| tierb-s1-{result,postalignment}.json + terminal-screen.png | **KEEP_FOR_ARCHIVE** | 修复链 FDP 证据（评审 §3 引用） |
| 全部 uniagent-emulator-*.md 结果文档 + graduation-review/decision | **KEEP_FOR_ARCHIVE** | 毕业证据链 |
| .dsh/…/state/dispatches/（dispatch records） | **KEEP_ACTIVE**（归档时随治理目录处置） | UniFlow 审计轨迹 |
| TIERB_DEBUG_EVIDENCE 诊断钩（FixtureSemanticEnvironment 内，env-var 门控） | **KEEP_ACTIVE** | 毕业评审 §4 D2 定位明确引用"evidence dump (seq 26-29)"——**有毕业 claim 依赖，不删**（§4 要求先证明无依赖才可删：证明失败） |
| RuntimeExplorationSemanticAdmission*GuardTests、StrategyExploration*Tests（4 新测试） | **KEEP_FOR_ARCHIVE** | RESAR graduation 证据组成部分 |

**TEMPORARY_DEBUG 待删项：零**（debug 钩有依赖；无其他临时文件残留——.e2e/、emulator
日志均在 /tmp 仓库外）。

## 7. OpenSpec Archive Readiness

| 项 | Phase 2（前驱+RESAR） | Phase 2.5 |
|---|---|---|
| tasks 真实完成 | 前驱 23/23（revocation 已回退受影响项后由 RESAR 收口）；RESAR **39/39** | **30/30**（含 checkbox 修复注记） |
| strict | PASS / PASS | PASS |
| artifacts 一致 | 一致（RESAR 明确不重写前驱冻结 spec） | 一致（S2 修订经 Human 授权随 change 内更新） |
| graduation decision | final-graduation-decision（GRADUATED） | graduation-decision（GRADUATED，Human APPROVED） |
| capability/non-claims 冻结 | baseline-freeze + final decision | graduation-decision §2/§3 |
| 未完成实现任务 | 0 | 0 |
| 剩余 Human Gate | 仅 archive | 仅 archive |
| active 文档引用 | current-gates 22 处 active 路径引用（归档时按既有 archive 惯例同步迁移） | 同左 |

**陷阱说明（已排除）**：tasks 数字不是依据——前驱 tasks 的 23/23 是"revocation 回退→RESAR
收口"后的终态，其历史在 tasks diff 与 reverification 决策中完整可溯。

## 8. Documentation Reference Audit

生命周期文档对 active change 路径的引用（current-gates 22 处、snapshot/roadmap 若干）均为
**"proposal/tasks 链接 + 状态注记"形态**——与既有 41 个已归档 change 的引用迁移惯例相同
（archive 时 current-gates 由生成规则重建）。无文档把 active 路径当**永久权威源**引用
（权威引用全部指向 docs/decisions/）。**无需归档前修引用**；唯一注意项见 §13-2。

## 9. Phase 3 Isolation Check：**通过**

`uniagent-local-exploration-memory`：draft-only（0 tasks 勾选）、0 处 Runtime 源路径引用、
所有 ExplorationLedger/GoalEvidence 提及均为**禁止条款**（"does not receive / keeps out"）、
无 Phase-2/2.5 实现依赖、无 archive 依赖。Phase 2/2.5 归档后 Phase 3 可从冻结基线独立开始。

## 10. Fresh Regression Baseline（本轮全部重跑，非复用）

| 项 | 数字 |
|---|---|
| build | 0 err / 0 warn |
| Runtime deterministic | **2109/2109** |
| Semantic | **32/32** |
| ValidationHarness | **56/56** |
| architecture guards | **61/61** |
| consistency / diff-check | ALL PASS / PASS |
| strict：前驱 / RESAR / Phase2.5 / Phase3-draft | PASS ×4 |

## 11. Phase2ArchiveReadiness

**READY**（前驱 + RESAR 一并归档；两者被同一 final graduation decision 覆盖）。

## 12. Phase25ArchiveReadiness

**READY**。

## 13. Cleanup Required Before Archive（两项，均为治理性质）

1. **server-mode post-archive 增量的显式化**：已归档 change 的 README/tasks 与
   PhysicalHostComposition/ServerModeIntegrationTests 的 graduation-repair 增量（0826 晚），
   应在归档批次中以其归属记录（例如在 final graduation decision 或 closeout 清单中加一行
   "driverhost-production-server-mode graduation repair included in this baseline"），
   避免未来读档时把 post-archive 编辑误判为漂移。**不建议**改已归档 bundle 本身。
2. **`.codex/config.toml` effort 变更（medium→high）的归属确认**：内容与本仓库 capability
   无关（harness 配置）；建议随归档 commit 一并纳入并在 message 中注明"tooling config，
   non-capability"，或由 Human 决定单独处置。

另记（无需动作）：`tmpDecision/dsh-uniflow-agent-loop-design.md` 为 UniFlow 插件设计决策，
属 H 类治理工件，随库保留。

### Provenance record (2026-08-26, unified archive batch)

1. **server-mode post-archive repair**: the archived change
   `uniclaw-driverhost-production-server-mode` (2026-08-21) carries a later
   graduation-repair increment in the current baseline: an injectable
   `RunGraphFactory` testability seam in `PhysicalHostComposition.BuildDriverHostServer`
   (+ `ServerModeIntegrationTests` coverage + the archived bundle's own
   README/tasks status rows, mtimes 2026-08-26 evening). Nature:
   post-archive implementation/testability correction; default production path
   behavior unchanged; NOT part of Phase 2 or Phase 2.5 capability; NOT Phase 2
   graduation drift. The archived bundle is left as history — this record is
   the authoritative provenance note.
2. **`.codex/config.toml`** (reasoning effort medium→high): Owner
   `DEVELOPMENT_TOOLING`; CapabilityOwnership `NONE`; excluded from Phase 2 /
   Phase 2.5 capability diffs, Runtime ArchitectureDelta, and archive
   capability evidence. Retained as user tooling config; not reverted.

## 14. Unified Archive Recommendation

**UNIFIED_ARCHIVE_READY_PENDING_HUMAN_GATE** —— 完成 §13 两项记录性清理后，可一次性归档：
`runtime-exploration-ledger-and-depth-control` + `runtime-exploration-semantic-admission-remediation`
+ `uniagent-emulator-validation-harness`（Phase 3 draft **不随批**，保持 active）。

## 15. Remaining Human Gates

1. 统一 Archive Gate（本审计的直接后继）。
2. Phase 3 Memory 独立 Human Gate（apply 授权；草案隔离与兼容已确认）。
3. Phase 4 NOT_AUTHORIZED 维持。

## 16. AuthorityDelta

`NONE`。

## 17. ArchitectureDelta

`NONE`（审计只读 + 本结果文档；未修改任何代码/契约/证据）。
