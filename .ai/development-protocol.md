# AI Development Protocol — UniClaw Agent Runtime

> 版本: v1.0 | 日期: 2026-08-08
> 定位: 与平台（Claude / Codex）和模型无关的共享开发协议。
> 读者: 所有进入本仓库的 AI Coding Agent，无论运行在哪个平台。
> 平台适配: Claude 适配层见 `CLAUDE.md` 与 `.claude/`；Codex 适配层见 `AGENTS.md` Codex 段。
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
- 突破 Phase Boundary 必须走 Human Gate
- 除非 Scenario 证明必然性，否则不得提前实现
- "方便后续开发"不构成突破 Boundary 的理由

---

## 7. Human Gate

只有以下情况必须停下来请求 Human Decision：

1. 必须修改 Architecture Invariant
2. mutable state 出现多个合理 owner（I-2 争议）
3. decision 出现多个合理 authority（I-3 争议）
4. 必须新增核心 architecture layer（第五 Spine 层）
5. Charter / OpenSpec 存在无法自行收敛的实质冲突
6. 必须突破 Phase Boundary
7. Scenario 证明 Architecture Contract 本身错误
8. 必须修改已 frozen 的 Human Gate Decision
9. Production complexity 没有 Scenario Receipt

**正常情况自主决定**：compile error 修复、private helper 提取、test fixture 组织、local naming、small refactor。

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

该规则的平台适配见各平台入口（Claude: `.claude/MCP-QUERY.md`；Codex: 使用等效语义工具）。

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

---

## 15. Frozen Decisions

已 frozen 的架构决策只能通过 Human Gate 修改。

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
| `docs/system/greenfield-runtime-charter.md` | 完整行为指导（60 节） |
| `docs/system/constitution/runtime-architecture-contract.md` | 14 条不可违反边界契约 |
| `docs/decisions/` | Human Gate 裁决 + Architecture Receipt |
| `docs/architecture/guards/` | Guard 文档（规则 / 证据 / 场景 / 违规示例） |
| `openspec/changes/` | 变更进度（system of record） |
| `CLAUDE.md` | Claude Code 适配层 |
| `.claude/` | Claude 专属工具配置 |
