# Harness V2 — Phase 1 Protocol Inventory（七类协议现状与双 Host 不一致）

> DocumentType: `HARNESS_MIGRATION_ANALYSIS`
> Status: `COMPLETE / EVIDENCE-BACKED`（只读调查产物）
> Authority: `NONE`（本文件不冻结任何 V2 契约；冻结发生在 Phase 3）
> Scope: Instruction / Skill / WorkItem / Routing / Result / Evidence / Session 七类协议的
> 定义处、语义、Codex 与 DSH 接入方式、不一致事实、与 V2 目标的差距。
> 调查基线: `uni-agent` 谱系（调查产物，对象仓库状态见 Phase 0 §0）；事实编号 F1-F15 引用 `harness-v2-phase0-inventory.md` §9。
> 禁止边界: 差距描述不是重写授权；Phase 3 契约冻结前不做大规模变更。

## 1. Instruction Protocol

- **定义处**：根 `AGENTS.md`（SSOT 入口 + Authority Order 7 级 + UniFlow 触发词 §4）；
  嵌套 AGENTS.md ×6（模块 map）；`CLAUDE.md`（Claude 纯指针，C9 强制）；
  `.ai/universal-agent-guideline.md`（8 行行为基线）；validator L189 解析目标路径
  `AGENTS.override.md`/`AGENTS.md` 进 Worker manifest。
- **Codex 接入**：原生读根 AGENTS.md；agent 角色指令内嵌于 TOML `developer_instructions`。
- **DSH 接入**：DSH 读入根 AGENTS.md（workspace instructions）；角色语义来自 profiles +
  dsh_profile_adapter 组合。
- **不一致**：UniFlow 触发语义写在通用入口（AGENTS.md §4）但两 host 的识别与后续
  行为由各自机制保证，无机械校验；Codex 侧角色指令是自含文本（改协议不会自动反映）。
- **V2 差距**：directive §3.1 要求 Root AGENTS.md = global dev contract、Nested = scoped
  module contract、不放临时任务信息——现状基本符合；主要差距是 AGENTS.md 的 Authority
  Order 第 2 位是 Approved OpenSpec Specs（OpenSpec 弃用后需改写）与「Where Is Truth」
  表中的 openspec 行。

## 2. Skill Protocol

- **定义处**：`.ai/skills/README.md`（canonical 声明：`.ai/skills/<name>/SKILL.md` 唯一
  正文源）；frontmatter description 为触发语义；work-flow §6 规定 `required_skills`
  有序数组 + fail-closed 解析（缺失/重复/非法/frontmatter 不匹配 → 拒绝）；Skill
  `Authority: NONE`，不进入约束优先级。
- **Codex 接入**：`.agents/skills/` 符号链接发现 + validator 只从 `.ai/skills` 解析
  （`SKILL_SOURCE_DIRS`，L17）。
- **DSH 接入**：`.dsh/skills/` 符号链接扫描 + dispatch payload 携带完整正文与
  fail-closed 指令（延迟派发必须原样保存 worker_payload，workflow §10）。
- **不一致**：两条消费路径各自实现，无统一一致性校验（F6）；外部生成的 openspec-×4
  格式与手工 Skill 发散（F15）；符号链接环境依赖 + 手动分批同步（F12）。
- **V2 差距**：directive §4 要求 `skills/<name>/{SKILL.md, agents/openai.yaml,
  references/, scripts/, tests/}` 结构——现状仅 evidence-driven-debugging 有 references/，
  无一有 scripts/ 或 agents/openai.yaml；确定性操作脚本化（scripts/ 优先）尚未发生。

## 3. WorkItem Protocol

- **定义处**：`.ai/schemas/work-item.schema.json`（17 字段）+ `.ai/task-contract.md`
  （其上的 Scenario 生命周期层）+ workflow §6。
- **字段全表**（schema 行号见 Phase 0 §2）：`id / change_set_id / base_revision /
  role_profile(enum: module-worker) / execution_profile(5 值) / module_profile(单字符串) /
  worker_owner / objective / required_skills(有序，向后兼容可空) /
  semantic_brief{summary≤240字, core_points 1-5×≤100字} / scope{write[], read_hints[]} /
  anchors[{path,symbol?}] / change_principles(≥1) / contract_refs / acceptance(≥1) /
  forbidden / escalation / leader_decisions_frozen(const true) /
  unresolved_architecture(maxItems 0)`。
- **强制规则**：单播禁 fanout；semantic_brief 优先级链（Contract→change_principles→
  forbidden→acceptance→scope→semantic_brief）；`core_points` 中强制词必须落在正式约束
  字段；中文精炼输出。
- **Codex 接入**：WorkItem 由 Leader 生成 → validator `work-item` 子命令校验 → spawn
  单 agent；TOML instructions 内嵌同一套字段语义（F3）。
- **DSH 接入**：`dispatch` 唯一入口，原子 dispatch record + worker_payload（含 Skill
  正文）；ResultGate 校验回执一致才接受 delta。
- **不一致**：Codex 侧无 payload 完整性机制（提示词约定）；schema 语义在 TOML 文本 ×4
  与 validator 与 dsh adapter 三处平行维护（F3）。
- **V2 差距**（directive §9/§10/§12）：缺 `dependencies`（依赖靠 H4-1 + 文字规则，F7）；
  `frozen_decisions` 对应物是 `change_principles`+`leader_decisions_frozen`（命名与
  语义需在 Phase 3 裁决）；`role_profile/execution_profile/module_profile/worker_owner`
  四字段按 directive §9 需重估（多人治理 vs Fresh Agent 自含性）；「垂直 tracer-bullet
  拆分」与「Self-contained but not self-bloated」未成文。

## 4. Routing Protocol

- **定义处**：`.ai/model-routing.yaml` v5（canonical_roles 2 + tiers 4 + providers +
  roles→tier + codex_profile_adapters ×4 + policy）；`.ai/agent-routing.md`（人读）；
  `.ai/profiles/{roles,execution,modules}.json`（2+5+5）；workflow §3/§7 路由表。
- **语义**：`LEADER DECIDES. WORKER EXECUTES.`；authority 来自角色不来自模型名；
  禁 silent downgrade；fallback 耗尽 → ROUTING_UNAVAILABLE。
- **Codex 接入**：TOML 硬编码 `gpt-5.6-luna` + config effort；`codex_profile_adapters`
  映射 4 个 TOML。
- **DSH 接入**：profile-source.yaml `model_bindings`（decision_frontier: zai glm-5.2 /
  fallback opencode-go；implementation_efficient / semantic_read: deepseek-v4-flash；
  tool_only: none）。
- **不一致**：共享 model-routing.yaml 内嵌 `codex:` host 块与 Codex TOML 路径引用
 （F4）——host 专有绑定泄漏进 canonical 层；双 host 实际绑定源不同且不对称（F5）；
  modules 5 vs 文档 4（F1）；effort 文档漂移（F2）。
- **V2 差距**（directive §23）：目标 `Capability → UniFlow Routing → Execution Profile →
  Harness Adapter → Concrete Model`，Skill 可声明 required capability 但不绑具体模型
  ——现状 canonical_roles 已 provider 中立，但 (a) host 绑定块混在共享文件，(b) 双 host
  绑定不对称，(3) 无 capability 声明机制。

## 5. Result Protocol

- **定义处**：`.ai/schemas/work-result.schema.json` + `.ai/result-contract.md`（325 行）。
- **WorkResult**：required `id/status/base_revision/changed/verification/
  module_context_delta/deviations/unresolved`；status enum = `DONE / BLOCKED_FOR_SPEC /
  BLOCKED_FOR_SEMANTIC_REVIEW / BLOCKED_FOR_ARCHITECTURE_REVIEW / BLOCKED_FOR_HUMAN /
  ROUTING_UNAVAILABLE`；`changed=[{path,symbols,summary}]`；
  `verification=[{command,result,evidence_ref}]`；`module_context_delta` 仅候选，Leader
  接受后生效。**无独立 evidence/tests 字段**（evidence 藏在 verification.evidence_ref）。
- **上层状态机**（result-contract）：Fast Lane（IMPLEMENTED/TEST_FAILED/REPAIR_REQUIRED/
  LOCAL_GAP/VALIDATION_PASS）+ FAST_LOOP_RESULT + AUTOPILOT_RESULT +
  HUMAN_DECISION_REQUIRED（5 字段 packet）+ VALIDATION_RESULT + PHASE_CONTROLLER_RESULT。
- **Codex 接入**：Worker 按提示词约定返回精简 WorkResult；无机械 gate。
- **DSH 接入**：`WorkResultGate` 机械校验（schema + binding 回执一致）。
- **不一致**：DSH 有机械 ResultGate、Codex 只有提示词约定——同 schema 两侧强制力不对称。
- **V2 差距**（directive §26 Result Compatibility）：统一规范化
  `status/changes/tests/evidence/unresolved/escalation` 六元组；「Worker 自述 ≠
  Completion Evidence」（现状已部分满足：DONE≠完成 F8，但 canonical 完成分散在
  result-contract 多个状态机中）。

## 6. Evidence Protocol

- **定义处**：development-protocol §17（L0-L4 任务分级、E0-E4 证据分级、Worker 7 步、
  输出含 Evidence used 等级）、§10 Scenario Receipt、§11 验证节奏；evidence-driven-
  debugging SKILL（E0-E4 定义）；openspec change evidence/ 目录惯例；docs/work/active/
  workitems + current-gates + snapshots/latest.md（GeneratedProjection，C11/C12 核对）。
- **Codex 接入**：Worker 自述 evidence_ref；Leader 独立核对（步⑩）。
- **DSH 接入**：receipt 从 Host session 日志重建核对（dispatch record ↔ 回执）。
- **不一致**：证据的机械可核对性两侧不同（DSH receipt vs Codex 自述+Leader 复核）。
- **V2 差距**（directive §8/§26）：工件逻辑收敛为 Decisions/Plans/WorkItems/Evidence
  四类——现状证据分散于 openspec evidence/、docs/work/、docs/snapshots、docs/decisions、
  L1-L4 资产，且再生机制（finalize→regenerate-projections）与 OpenSpec 绑死（F10）。

## 7. Session Protocol

- **定义处**：workflow §5（自动上下文 manifest）、§8（ProfileContextKey 缓存：键一致可
  复用 Worker thread；revision 变化/blocked 必重载）；task-contract L145-149
  （runtime-validator fresh reload 独立性）；agent-branch-workflow（worktree 隔离）；
  .dsh/profile-adapter（`state/sessions/<sid>/runs/<rid>/`、PENDING_SESSION_SPAWN、
  checkpoint）；.codex/config.toml（threads ≤8）。
- **Codex 接入**：Codex threads；ProfileContextKey 复用稳定 prompt 前缀。
- **DSH 接入**：run 级状态布局 + 延迟 spawn 时 dispatch record 携带完整 payload。
- **不一致**：两侧会话模型不同（threads vs run-scoped state），但各自在 adapter 内——
  无共享层污染。
- **V2 差距**（directive §11）：`One WorkItem = One Disposable Execution Context`——
  现状 ProfileContextKey 的**复用**方向与 fresh-context 目标相反（F9）；「全新 session +
  WorkItem + references 可正确继续」的验收尚未定义机械测试。

## 8. Codex / DSH 不一致汇总

| 维度 | Codex | DSH | 不一致性质 |
|---|---|---|---|
| 角色指令 | TOML 自含文本（×4 重复） | profiles + adapter 组合 | 语义三处平行维护（F3） |
| Skill 消费 | validator 直读 canonical | 扫描 .dsh/skills + payload 正文 | 无统一校验（F6） |
| 模型绑定 | TOML 硬编码 Luna | profile-source model_bindings | 绑定源不对称（F5），共享文件含 codex 块（F4） |
| Result 强制 | 提示词约定 | ResultGate 机械校验 | 强制力不对称 |
| Evidence | 自述 + Leader 复核 | receipt 重建核对 | 机械可核对性不同 |
| 会话 | threads + ProfileContextKey 复用 | run 级状态 + 原子 dispatch | 各在 adapter 内，无共享污染 |
| 机器可读适配配置 | 无（语义埋在 TOML 文本） | profile-source.yaml（sha256 钉扎） | **结构性不对称**：DSH 侧有单一配置面，Codex 侧没有 |

## 9. V2 目标生命周期映射（directive §7 vs 现状）

| V2 阶段 | 现状最近对应 | 缺口 |
|---|---|---|
| Intent | 用户触发词 + Reality Preflight 前半 | 无显式命名/产物 |
| Explore | Reality Preflight + openspec-explore skill（域外） | 不在 UniFlow 主流程内 |
| Decision | Leader 12 步 ⑥冻结 + H4-1 decision_type | 与 Plan 合并；决策工件=OpenSpec change（将弃用） |
| Plan | ⑥-⑦（principles+WorkItem 生成） | 无独立 Plan 工件 |
| WorkItems | WorkItem schema + 单播派发 | 无 dependencies/DAG（F7）；无 tracer-bullet 垂直拆分原则 |
| Fresh Context | §8 缓存（反向）+ validator fresh reload | 方向相反（F9） |
| TDD / Diagnose | §17 L 分级 + E0-E4 + test-authoring profile | TDD 非显式纪律；debugging 三入口 |
| Review | change-review 四象限（提交前） | 无 Fresh Review Context 概念 |
| Verify | acceptance + test_gates + Leader 核对 | 与 Review 重叠 |
| Complete | result-contract 多状态机 + Leader canonical | 判定分散（F8） |

**双轨生命周期事实（F13）**：UniFlow（Profile/WorkItem 派发）与 H4-1/2/3（Scenario
循环：trigger→decision→auto-continue）是两套并行协议，仅靠 contract_refs 与 Leader
角色衔接。V2 必须裁决合并方式——这是 Phase 3 最大的契约冻结项之一。

## 10. Phase 3 待冻结项（由本清单差距推出，供决策）

1. WorkItem V2 字段集：`dependencies`、`frozen_decisions` 命名、四 profile 字段去留。
2. H4-1/2/3 三契约与 UniFlow 的合并/降级方式。
3. model-routing.yaml 的 host 块剥离（codex: 块移入 .codex 适配配置，对称 DSH
   profile-source）。
4. Result 六元组规范化（status/changes/tests/evidence/unresolved/escalation）。
5. Fresh Context 政策（ProfileContextKey 复用语义改写或废除）。
6. check-consistency C11/C15 的 OpenSpec 解绑（最硬锁点，F10/F12 前置）。
7. 证据工件四分类（Decisions/Plans/WorkItems/Evidence）的目录决定。
8. Cross-harness 兼容测试的形态（同 WorkItem 双执行对比，directive §26）。

—— 证据来源：Phase 0 §2-§9 + subagent B/D 报告（字段表、行号、双 host 接入方式）。
