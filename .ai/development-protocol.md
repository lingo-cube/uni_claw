# AI Development Protocol — UniClaw Agent Runtime

> 版本: v1.0 | 日期: 2026-08-08
> 定位: 与 AI Coder Host 和模型无关的共享开发协议。
> 读者: 所有进入本仓库的 AI Coding Agent，无论运行在哪个平台。
> 平台适配: 统一入口见 `AGENTS.md`（Single Source of Truth）与 `.ai/`；`.agents/skills/`、`.codex/`、`.dsh/` 和根 `CLAUDE.md` 仅适配 Host 发现或调用机制。
> 模型路由: 见 `.ai/model-routing.yaml`（档位定义、provider 映射）与 `.ai/agent-routing.md`（角色调度）。

---

## 1. Authority Order

所有 AI Coding Agent 必须遵守以下优先级。低优先级规则不得覆盖高优先级规则：

```
1. Architecture Invariants（I-1..I-14，docs/system/constitution/runtime-architecture-contract.md）
2. Approved OpenSpec SHALL（openspec/changes/<change>/specs/**/*.md）
3. Approved Active Scenario acceptance constraints（scenarios/*.md）
4. Domain design rules（docs/system/greenfield-runtime-charter.md，60 节）
5. General design principles（SOLID、高内聚低耦合等）
6. Implementation preferences
```

Invariant 不能因为"这样更灵活"或"测试写起来不方便"而被修改。

---

## 2. Repository Truth

Repository 是唯一真相来源。以下优先级递减：

- `docs/system/constitution/runtime-architecture-contract.md` — 14 条不可违反的边界契约
- `docs/system/greenfield-runtime-charter.md` — 60 节完整行为指导
- `openspec/changes/<change>/` — proposal / design / specs / scenarios / tasks
- `docs/decisions/` — Human Gate 裁决与 Architecture Receipt
- `src/UniClaw.Runtime/` — 生产实现
- `tests/UniClaw.Runtime.Tests/` — 机械 Guard 与场景测试

聊天上下文、dispatch 摘要、旧对话不得覆盖 Repository。如果发现矛盾，以 Repository 为准。

---

## 3. Scenario-First Principle

任何 production capability 必须由 Active Scenario 购买。

**规则**：
- 每个新增的 production model / component / field 必须有对应的 Scenario Evidence Required
- 没有 Scenario 购买的能力是 I-12 违约
- "以后会需要"不构成实现理由

**格式**：
```
Scenario → Missing capability → New model → Protected invariant
```

**证据**：Scenario Receipt Audit（Phase 2 precedent: `docs/decisions/phase2-scenario-receipt-audit.md`）。

## Two-Lane Development Model

UniClaw development uses two lanes. The Project Leader selects the lane from
repository truth:

```text
NEW SEMANTICS → SLOW GOVERNANCE
ACCEPTED SEMANTICS → FAST DELIVERY
```

Both lanes use provider-neutral logical roles (`PROJECT_LEADER_MODEL` and
`EXECUTION_WORKER_MODEL`). Provider-specific model identifiers are resolved
from `.ai/model-routing.yaml`.

### Lane A — Semantic Discovery Lane

Use `SEMANTIC_DISCOVERY` when any of the following is true:

1. New Reality Pressure is suspected.
2. The existing Reality Model cannot explain observed reality.
3. A new Capability Primitive may be required.
4. Existing capability semantics are insufficient or ambiguous.
5. Ownership or decision authority may need to move.
6. Dependency direction or an architecture invariant may change.
7. Safety authorization semantics may change.
8. Completion, recovery, or world-truth semantics may change.

The canonical flow is:

```text
Evidence → Reality Distinction → Canonical Pressure → Reality Model
→ Validation / Admission → Capability Gap → Capability Candidate
→ Human / Semantic Gate → Architecture Challenge if needed
```

This lane is cautious, serial in semantic commitment, and gate-driven. Research
workers may run in parallel, but no worker may commit semantic, architecture,
ownership, authority, or invariant decisions.

### Lane B — Capability Delivery Fast Lane

Use `CAPABILITY_DELIVERY_FAST` only when the relevant CP and RM (where required)
are accepted, the capability gap is established, the capability semantics are
approved or frozen, and the requested work remains inside current architecture
invariants.

The default loop is:

```text
Accepted Semantic Need → Minimum Falsifying Scenario → Architecture Fit Check
→ Minimum Implementation → Executable Verification → Diagnose → Repair
→ Re-run → Freeze
```

Ordinary implementation, test, documentation, style, lint, and bounded
deterministic validation failures auto-continue inside the authorized scope.
They do not create a Human Gate by themselves.

### Architecture Fit Check

Before implementing an accepted capability, ask only whether the capability can
be represented honestly in the current architecture:

- mutable state ownership unchanged;
- decision authority unchanged;
- dependency direction unchanged;
- architecture invariants unchanged;
- safety authority unchanged;
- external-world authority unchanged.

If all answers are yes, record `ARCHITECTURE_FIT_CONFIRMED` and continue
automatically. This check is not an Architecture Design Review and does not
create an architecture ceremony when no boundary changes.

If any answer is no or materially uncertain, return
`ARCHITECTURE_GATE_REQUIRED` and stop the Fast Lane.

### Fast Lane Failure Policy and Hard Gates

Inside an authorized Fast Lane, the Project Leader diagnoses and repairs bounded
failures, then re-runs validation. This includes:

- `MECHANICAL_FAILURE`;
- `TEST_FIXTURE_FAILURE`;
- `LOCAL_BEHAVIOR_GAP`;
- `LOCAL_COMPOSITION_GAP`;
- purchased-semantic `ASSERTION_MISMATCH`;
- `DOC_RECONCILIATION`;
- local implementation `BUILD_FAILURE`;
- repairable implementation regressions;
- style, lint, static, and bounded deterministic test failures.

The Fast Lane stops only for a real boundary event:

| Hard Gate | Trigger | Result |
|---|---|---|
| `HG-SEMANTIC` | Reality contradicts the accepted RM, a new fact cluster appears, or semantics must expand beyond authorization | `NEW_SEMANTIC_PRESSURE`, `NEW_REALITY_MODEL_REQUIRED`, or `SEMANTIC_GATE_REQUIRED` |
| `HG-ARCHITECTURE` | New layer, ownership/authority transfer, dependency-direction change, or invariant modification is required | `ARCHITECTURE_GATE_REQUIRED` |
| `HG-SAFETY` | Safety authorization or irreversible/risky-action semantics must change | `SAFETY_SEMANTIC_GATE_REQUIRED` |
| `HG-HUMAN` | Repository governance reserves the next irreversible boundary decision for a human | `HUMAN_GATE_REQUIRED` |
| `HG-VALIDATION` | Required validation cannot be satisfied, or resolution needs semantic judgment | `VALIDATION_BLOCKED` |
| `HG-SCOPE` | The smallest correct implementation exceeds the authorized capability | `AUTHORIZED_SCOPE_EXCEEDED` |

Implementation pressure may flow upward into Semantic Discovery. Semantic
uncertainty must never be silently normalized into implementation. When a Hard
Gate is reached, preserve executable evidence, record the failed assumption,
exit the Fast Lane, resolve only that exact pressure in Semantic Discovery, and
return to the same Fast Lane afterward.

### Human and Project Leader Roles

Human interaction is required only at the material boundaries defined below:
invariant, ownership/authority, safety-semantic or material public semantic/API
change, unresolved product alternatives, contradicted owner prior, or
significant complexity/budget expansion. It is not required for ordinary
worker dispatch, local implementation choices, test repair, repeated validator
runs, or documentation reconciliation when existing authorization covers the
work.

The Project Leader selects the lane, preserves the accepted semantic envelope,
dispatches workers, integrates evidence, owns loop continuation, detects Hard
Gates, maintains repository truth, and completes validation. Final semantic and
architecture authority must remain with the Project Leader / required Human
Gate, never with a worker.

### Human-Compressed Governance

Machine-facing governance remains detailed. CP / RM / WF / RI / ER records,
Scenario Contracts, validation/admission receipts, provenance, architecture
evidence, and executable verification stay in repository truth.

When Human authority is genuinely required, the Project Leader compresses that
detail into exactly one decision packet:

```text
Goal
What changed / was discovered
Architecture impact
Material trade-off
Exact decision required
```

The Human is not required to review routine provenance normalization, evidence
label repair, deduplication, mechanically resolvable conditional-pass findings,
local implementation choices, ordinary test/build failures, or bounded repair.
Those remain machine-facing work owned by the Project Leader and bounded
workers.

### Owner Architecture Prior — Fast Falsification

`OWNER_ARCHITECTURE_PRIOR` records a Human architecture judgment as a
high-priority hypothesis, not automatic repository truth.

```text
Human Prior
→ Project Leader performs bounded falsification against repository evidence
→ no material contradiction: adopt as the working direction
→ material contradiction: preserve exact evidence and request Human judgment
```

The Project Leader must test the nearest falsifier first and must not restart
full semantic discovery merely to rediscover a plausible prior. A prior cannot
override an invariant, accepted semantic receipt, executable observation, or
frozen authority boundary. Worker findings remain evidence; only the Project
Leader adopts or rejects the working direction.

### Semantic Discovery Autopilot

`SEMANTIC_DISCOVERY_AUTOPILOT` preserves the Semantic Discovery lane while
removing routine Human relay. For one explicitly selected pressure/capability
boundary, the Project Leader may autonomously execute:

```text
Evidence research
→ Reality Model extraction
→ independent validation
→ condition repair
→ admission
→ capability-gap analysis
→ candidate generation
→ Architecture Fit
```

Every semantic commitment still requires repository evidence, fact/inference
separation, provenance, falsifiers, deduplication, independent validation, and a
fresh repository read. Detailed artifacts remain machine-facing. Autopilot does
not authorize automatic selection of an unrelated next Scenario or capability.

The loop stops for Human input only when one of these material boundaries is
reached:

1. architecture invariant change;
2. mutable-state ownership or decision-authority change;
3. safety-semantic change;
4. material public semantic/API expansion;
5. two legitimate product-level alternatives with no repository-backed winner;
6. an `OWNER_ARCHITECTURE_PRIOR` is materially contradicted by evidence;
7. significant complexity or budget expansion.

Routine normalization, labeling, deduplication, condition repair, admission
mechanics, local implementation judgment, and ordinary validation failures do
not create a Human Gate. A semantic, architecture, safety, validation, or scope
gate may still stop execution without necessarily requiring Human input.

### Test Asset Evolution Feedback Loop

The canonical development feedback loop is:

```text
Run → Evidence → Asset triage → Reproducible minimal case
→ Short-chain integration asset → Replay / regression corpus
→ Coverage / failure clustering → Next capability pressure
→ Implementation → New runs
```

A meaningful behavior or failure should preferably be preserved as the
smallest executable short-chain integration asset that still crosses the real
production boundaries responsible for the pressure. Never mock away the layer
that caused the pressure.

| Level | Purpose |
|---|---|
| `L1_ATOMIC` | Local rules/types; supporting evidence only |
| `L2_SHORT_CHAIN_INTEGRATION` | Primary regression asset; crosses the minimum responsible production boundaries |
| `L3_RECORDED_REALITY_REPLAY` | Replays external/emulator-derived evidence through production-shaped semantics |
| `L4_LIVE_EMULATOR_DEVICE` | Reality calibration and high-value end-to-end evidence |

Prefer corpus growth in L2 and L3. Full live end-to-end coverage is not required
for every regression.

Every triaged run uses exactly one classification:

```text
KNOWN_REGRESSION | NEW_VARIANT | NEW_EVIDENCE | NEW_FAILURE_MODE
| POSSIBLE_NEW_PRESSURE | NOISE_OR_DUPLICATE
```

Promote an asset only when it is reproducible, novel or stronger evidence,
material to correctness/usability/safety, retains the pressure after
minimization, and has an explicit PASS/FAIL oracle. A meaningful production
failure is not fully closed until a replayable regression asset exists where
feasible.

Future priority recommendations are evidence-pulled from regression failures,
asset clusters, coverage gaps, evidence-maturity gaps, false-success severity,
usability blockers, and safety impact. Static roadmap order remains guidance,
not automatic priority. The Project Leader alone commits corpus promotion and
next-capability priority; workers may prepare assets and recommendations.

---

## 3. Provider-Neutral Model Routing

### Canonical Logical Roles

All development protocols reference two provider-agnostic logical roles.
Provider-specific model identifiers are resolved from `.ai/model-routing.yaml`
and must not be hardcoded in protocol documents.

| Role | Tier | Authority | Responsibility |
|------|------|-----------|----------------|
| `PROJECT_LEADER_MODEL` | `leader` (HIGH_REASONING) | Canonical decisions | Lane selection, semantic commitment, worker dispatch, auto-continue, Architecture Fit, Gate judgment, scope, ownership, authority |
| `EXECUTION_WORKER_MODEL` | `fast`/`standard` (LIGHTWEIGHT) | Bounded execution | Implementation, testing, diagnosis, repair, evidence collection, docs reconciliation |

### Provider Mapping

```text
OpenAI:
  PROJECT_LEADER_MODEL → GPT-5.6 Sol
  EXECUTION_WORKER_MODEL → GPT-5.6 Luna

Anthropic provider:
  PROJECT_LEADER_MODEL → Opus
  EXECUTION_WORKER_MODEL → Haiku
```

This is a ROLE mapping, not a claim of technical identity between models.

### Core Principle

```text
LEADER DECIDES. WORKER EXECUTES.
```

The model provider does not determine responsibility. Roles are defined first,
then mapped to concrete provider/model identifiers. Changing the provider must
not change architecture invariants, semantic authority, Human Gate rules, Hard
Gate rules, auto-continue semantics, or authority boundaries.

### Worker Decision Limits

Execution Workers must not independently commit: new CP, new Reality Model,
semantic expansion, architecture expansion, ownership transfer, authority
transfer, dependency-direction change, invariant change, safety semantic change,
or scope expansion. Workers may detect or recommend these; they must return
evidence plus the exact escalation reason to `PROJECT_LEADER_MODEL`. A worker
escalation request is not a Hard Gate decision.

### Mixed-Provider Support

The logical role model permits mixed-provider execution (e.g., Anthropic Opus
Leader → GPT-5.6 Luna Worker) provided role authority remains unchanged and
worker capability is sufficient for the bounded task. Provider identity does
not imply authority — authority derives from role, not model name.

### Routing Priority

```text
1. Determine required ROLE
2. Resolve configured provider
3. Resolve concrete model
4. Execute

Never: model name → infer authority
Always: authority role → model mapping
```

---

## 4. OpenSpec Spec-Driven Lifecycle

所有变更走 OpenSpec 生命周期：

```
propose → apply → verify → archive
```

- **propose**: 创建 `openspec/changes/<change>/` 下的 proposal / design / specs / scenarios / tasks
- **apply**: 按 tasks.md 逐项实施，完成一项立即勾选 `- [x]`
- **verify**: 对照 specs 与 scenarios 验证；关键 slice 完成后走 independent validation
- **archive**: 提取 decisions，同步文档

不在 OpenSpec change 中的工作需明确说明。

---

## 5. Architecture Invariant Protection

以下架构不变量在整个开发过程中不可违反：

### Ownership (I-2)
- 一个 mutable state 只有一个 owner
- 跨 owner 边界只传不可变快照或消息
- 禁止共享可变对象跨 owner 传递

### Authority (I-3)
- 一个 decision 只有一个 authority
- 其他组件只能提供 evidence，不能重复判定

### Dependency Direction (I-1)
- Agent → Container → Traversal → Environment 是核心运行责任方向
- 反向依赖需要显式论证

### Observation (I-4)
- Observation 是 evidence，不是 semantic truth
- 观测结果不直接等同于世界事实

### Completion (I-10)
- 完成必须由 Goal Evidence 证明
- "Plan 耗尽"或"Action 返回成功"不是完成证据

### FSM (I-7)
- 状态机只做 protocol transition，不做 intelligence
- 业务智能决策在 FSM 之外

### Recovery (I-9)
- Recovery 是 act → observe → verify → reconcile 闭环
- 不是单个 PressBack 动作

### Escalation (I-8)
- Lower scope 可以向上 escalate，但不能偷偷取得更高 scope 的 authority
- 低层组件无法解决时显式上报，由高 scope 决策

详见 `docs/system/constitution/runtime-architecture-contract.md`（14 条完整不变式）。

---

## 6. Phase Boundary Discipline

Phase Boundary 是 frozen architecture decision。每一 Phase 明确定义了 Deferred（不得提前实现的能力）。

**当前 Phase 2 Deferred**（不得在 Phase 2 引入，Phase 3 购买）：
- Popup recovery / Container-scope recovery
- Uncertain action（派发后 timeout 重试）
- Scroll identity
- Fingerprint 字段与机制
- Dynamic Grounding
- coordinate / hierarchy model

**当前 Phase 3 Deferred**（不得在 Phase 2/3 引入，需后续 Phase 购买）：
- 真实设备 / Vision Adapter
- Semantic Identity 算法
- LLM / VLM / Memory
- DI 容器

**规则**：
- 突破 Phase Boundary 必须先进入准确的 Semantic / Architecture / Scope Gate；
  只有触及本协议七类 material boundary 时才升级到 Human Gate
- 除非 Scenario 证明必然性，否则不得提前实现
- "方便后续开发"不构成突破 Boundary 的理由

---

## 7. Human Gate

Human Decision 只用于本协议 `Semantic Discovery Autopilot` 列出的七类
material boundary：invariant、ownership/authority、safety semantics、material
public semantic/API expansion、两个同样成立的产品级选择、Human architecture
prior 被证据推翻、或显著 complexity/budget expansion。

Phase Boundary、frozen Human decision、Charter/OpenSpec 冲突或没有 Scenario
Receipt 的 production complexity，先按其实际压力分类；只有当解决方案落入
上述七类之一时才请求 Human。否则由 Project Leader 进入对应 Semantic、
Architecture、Validation 或 Scope Gate，不把每个 Gate 都升级为 Human Gate。

Human Gate 输出必须使用 `Human-Compressed Governance` 的五字段 decision
packet。详细 provenance、CP/RM、Scenario 和验证材料留在 repository artifact，
不要求 Human 逐项复核。

**正常情况自主决定**：provenance/label/dedup 修复、mechanical conditional-pass
repair、compile error、private helper、test fixture、local naming、small refactor、
ordinary test failure 和 bounded regression repair。

---

## 8. Failure Classification

Scenario Test FAIL 后不要立即 patch production。先分类：

| 类别 | 含义 | 处理 |
|------|------|------|
| IMPLEMENTATION | 现有 approved contract 足够，实现错误 | 最小 production fix → retry same task |
| TEST_HARNESS | Runtime behavior 正确，test infrastructure 不足 | 只修改 tests / fake |

不可自行回到 Human Gate | |

禁止无限 patch：同一 task 重复出现同类 failure 时重新分类，不能一直把 architecture / spec 问题当 implementation 修。

---

## 9. Contract Protection

整个开发循环中不得为了让测试通过：

- 修改 Architecture Invariants
- 偷改 Scenario Given / When / Then
- 偷改 Goal Evidence
- 把 Observation 变成 semantic truth
- 把 ActionResult 当 world result
- 建立 duplicate mutable-state owner
- 建立 duplicate decision authority
- 让 Traversal / Container 偷取 Agent authority
- 向 production 泄漏 Fake / ScriptedEnvironment 内部状态
- 硬编码场景字符串到通用 Runtime
- 提前实现 Phase Boundary 中的 Deferred capability

---

## 10. Scenario Receipt Rule

任何 production delta 必须能回答：

```
Production Element:  xxx
Purchased by Scenario: SC-P2-xxx
Required Semantic: xxx
Assertion: xxx
Why existing model insufficient: xxx
```

如果回答不了 → REJECT / DEFER。

Phase 2 precedent: `docs/decisions/phase2-scenario-receipt-audit.md`（11 项审计，全部有 Scenario Receipt）。

---

## 11. Verification Rhythm

| 层级 | 检查 |
|------|------|
| Per task | targeted build + targeted tests |
| Per task gate | affected Scenario assertions + relevant Architecture Guards |
| Per slice | full build + all tests + scenario tests + consistency + guard delta |
| Per phase | independent validator (runtime-validator) |

Meaningful behavior/failure evidence also receives asset triage under the Test
Asset Evolution Feedback Loop. L2 short-chain integration is the default
regression target; use L3/L4 only when recorded or live reality is necessary to
preserve the pressure.

机械检查：
- `dotnet build src/UniClaw.Runtime.sln` → 0 warnings, 0 errors
- `dotnet test src/UniClaw.Runtime.sln` → all green
- `scripts/check-consistency.sh` → ALL PASS

---

## 12. Development Flow

```
Requirement
  → Scenario（Scenario-first: 定义 Given / When / Then / Evidence）
  → Responsibility（哪个组件负责）
  → Authority（谁有决策权）
  → State Owner（唯一 mutable state owner）
  → Interfaces（Port 定义，如有外部依赖）
  → Implementation（最小实现）
  → Verification（对照 Scenario assertions）
```

不要 Prompt → immediately code。

---

## 13. C# Code Query Rule

查询 C# 代码（定义、引用、继承、诊断）时，**始终先用语义工具定位**（Roslyn / LSP / MCP），再按需读片段。

**禁止**用 `grep` / `find` 定位 C# 符号。

通用查询指南见 `.ai/tooling/csharp-mcp-query.md`；Host 没有对应语义工具时必须显式记录能力限制，再使用最小文本检索兜底。

---

## 14. Autonomous Execution Loop

Phase 执行遵循 Planner Loop：

```
Evolution Controller → select one task
  → Coder: implement exactly one task
  → Task Gate: verify assertions + guards
  → Evolution Controller → select next task
  → Loop until slice complete
```

- 一次只 dispatch 一个 task
- Coder 只做分配的 task，不自己选择下一 task
- Main session / project-leader 不替 specialized agent 做其职责
- BLOCKED 状态不得被改写为 DONE

For `SEMANTIC_DISCOVERY_AUTOPILOT`, the Project Leader similarly advances one
repository-backed stage at a time, delegates bounded evidence/repair work, and
re-reads repository truth before each canonical commitment. The loop may cross
routine governance stages autonomously but never crosses one of the seven Human
boundaries above.

---

## 15. Frozen Decisions

已 frozen 的架构决策不得由 routine repair 修改；需要变更时必须进入对应
material Human Gate。

当前 frozen（Phase 2）：
- Trap 7-field model (HG-2)
- Recovery ownership split (HG-4 Option B)
- Recovery → Container/Traversal forbidden (Guard 7)
- No RecoveryRequest/Planner/Runtime (HG-5)
- No DriftStatus field (HG-3)
- 11 Scenario Receipts on file

Phase 3 不得在没有新的 Scenario + Semantic Gate + OpenSpec reconciliation 的情况下修改以上决策。

---

## 16. Document Map

| Document | Purpose |
|----------|---------|
| `AGENTS.md` | 项目共享入口（map, not manual） |
| `.ai/development-protocol.md` | 本文件 — 共享开发规则 |
| `.ai/agent-routing.md` | 角色定义 + 调度规则 |
| `.ai/model-routing.yaml` | 模型档位配置 |
| `.ai/skills/evidence-driven-debugging/SKILL.md` | 通用证据驱动工作流（E0-E4 分级、Worker 流、Test Design） |
| `.ai/skills/runtime-behavior-debugging/SKILL.md` | Runtime 专属调试方法（失败分类、真机/非确定） |
| `.ai/reviews/change-review.md` | Runtime / Test / Architecture 变更评审清单 |
| `docs/system/greenfield-runtime-charter.md` | 完整行为指导（60 节） |
| `docs/system/constitution/runtime-architecture-contract.md` | 14 条不可违反边界契约 |
| `docs/decisions/` | Human Gate 裁决 + Architecture Receipt |
| `docs/architecture/guards/` | Guard 文档（规则 / 证据 / 场景 / 违规示例） |
| `openspec/changes/` | 变更进度（system of record） |
| `.agents/skills/` | 通用 Skill 发现 adapter（仅相对符号链接） |
| `.codex/` / `.dsh/` | Host 调用 adapter；不维护协议真相 |
| `CLAUDE.md` | 仅指向 `AGENTS.md` 的无状态兼容入口 |

---

## 17. Evidence-Driven AI Coding Workflow

> 目标：让 Worker Agent 默认遵循
> **Evidence → Diagnosis → Ownership → Minimal Change → Validation**。
> 适用 Skill：`.ai/skills/evidence-driven-debugging/`（通用方法论）与
> `.ai/skills/runtime-behavior-debugging/`（Runtime 专属应用）。
> 本协议是统一规则（Task Classification → Evidence Requirement → Execution
> Rules → Validation Rules → Review Rules）；Skill 提供执行细节。

### 17.1 Task Classification（任务风险等级）

Worker 在开工前按风险分级；L0 不强制 Evidence workflow，L4 必须完整 E4：

| 等级 | 任务 | 要求 |
|------|------|------|
| L0 | 文档、格式、简单修改 | 无需 Evidence workflow |
| L1 | 普通代码修改 | 明确目标、影响范围、测试 |
| L2 | 模块行为修改（状态、异步、数据流） | E1-E2 evidence |
| L3 | Runtime/Architecture 修改（Agent、FSM、Traversal、Semantic、Recovery、Lifecycle） | E3 evidence + `AuthorityDelta` + `ArchitectureDelta` |
| L4 | 系统集成修改（Real Device、E2E、Flaky、Environment） | E4 evidence |

Evidence 等级定义见 `.ai/skills/evidence-driven-debugging/`（E0: compiler
message … E4: trace timeline + observation frames + environment state +
action history + reproduction context）。

### 17.2 Execution Rules（Worker 默认流程）

所有 L2-L4 任务执行 7 步：

```
1. Identify scope（架构边界/owner 图）
2. Identify evidence（trace、observations、action history、ledgers）
3. Identify owner（哪个 seam 拥有该 decision/state）
4. Design minimal change（owner seam 内最小修改）
5. Implement
6. Validate invariant（authority / DFS ownership / GoalEvidence / 无场景知识）
7. Regression（相关套件 + 全量 + 真机复验，按 L 级）
```

Failure 分类（先证明，不猜测）：Discovery / Grounding / Authorization /
Execution / Recovery / Environment。禁止归因捷径：
"Child missing ⇒ DFS bug"、"Element missing ⇒ Semantic bug"、
"Test fail ⇒ Production bug"。

### 17.3 Output Format

L2-L4 任务输出 `PROJECT_LEADER_<TASK>_RESULT`，必须包含：

```
- Decision
- AuthorityDelta（NONE | CHANGED）
- ArchitectureDelta（NONE | ADDITIVE | BREAKING）
- Evidence used（等级 + 具体证据）
- Change summary
- Validation result（回归数字）
- Remaining risk
```

### 17.4 Validation Rules

- 测试验证**能力**，不验证脚本：禁止固定点击数量、固定 ActionHistory、
  固定页面路径、固定坐标、固定 UI 文案。
- 推荐 EvidenceFixture + ExpectedSpecification → Runtime Execution →
  Evidence Evaluation，验证 coverage / authorization / consistency /
  fail-closed / evidence sufficiency。
- 机械检查：build 0 error、相关套件全绿、`scripts/check-consistency.sh`
  ALL PASS、`git diff --check` clean（按 §11 Verification Rhythm）。

### 17.5 Review Rules

Runtime / Test / Architecture 变更提交前执行
`.ai/reviews/change-review.md` 四象限检查：

- **Authority**：是否改变责任边界 / 新增执行权限 / 绕过 Agent-FSM-GoalEvidence？
- **Evidence**：是否基于事实（按 L 级证据）？隐藏假设是否显式声明？
- **Boundary**：是否引入错误依赖 / 场景知识 / 把 Fixture 变生产逻辑？
- **Testing**：是否验证能力而非实现？是否存在脚本化测试？

结论 APPROVE / APPROVE-WITH-NOTES / REJECT（Authority 违规、场景知识、
脚本化测试 = blocking）。

### 17.6 STOP

立即停止：需要修改 Runtime production code、需要修改 Architecture
authority、需要强制所有简单任务（L0）走 Evidence workflow。
