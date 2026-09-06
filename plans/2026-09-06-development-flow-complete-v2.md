# 方案 — Development Flow 完备版 v2.1

> PlanType: ARCHITECTURE_PLAN · Status: DRAFT / AWAITING_APPROVAL
> 取代：plans/2026-09-06-development-flow-complete-v2.md（v2 整体并入 + 所有者
> 六项裁决 + 三处加固）
> 输入：Development Flow v1 directive + Matt 25-skills 视频洞见 + 所有者
> 2026-09-07 调整裁决（RESUME 入口协议 / 可复现 VERIFY / 失败转移现补；
> LEARN 钩子化；并发与 Metrics 延后）
> 复杂度：DECISION-HEAVY

## 0. 最终流程形态（冻结候选）

```text
ENTER / RESUME（入口协议，非状态）
  ↓
UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT → REVIEW → VERIFY → CLOSED
                                       ↖—————— failure edges ——————↙
                                                                ↓（可选）
                                                      LEARNING HOOK（非状态）
```

八状态主干不动；两个端点均为协议/钩子而非状态。

## 1. Entry / Resume Protocol（现补）

任何 agent（含全新会话）进入一个 Change 前执行：

```text
1. 读 changes/<id>/state.md（WHAT/WHY/ACCEPTANCE/status）
2. 复验（不信 state.md 本身）：
   a. git 实际状态 vs 声明的 base/进度
   b. 引用的 evidence 文件存在且内容匹配
   c. status 与仓库实际进度一致
3. 三者一致 → 从该状态继续
   任一矛盾 → 回 UNDERSTAND（先搞清分歧，不带着矛盾执行）
```

## 2. 不确定性路由 + Bug 旁路（不变，v2 §2 原文有效）

六路由表（grill-with-docs / domain-modeling / research / prototype /
diagnosing-bugs / 无未知直行）；AUTOMATIC FIRST。

## 3. 持久化（Durability ALWAYS, Depth VARIABLE，v2 §3 原文有效）

三档深度；Change State = WHAT/WHY/ACCEPTANCE，非 Plan/Ticket/ADR。
**MINIMAL 档校准**（会话评估结论）：允许 = 结构化 commit message 约定，
不强制独立文件——单行修复的 ceremony 成本必须近零。

## 4. 可复现验证声明（现补，取代「命令化」提法）

STANDARD+ 的 Change State 必须含 verification 声明，格式：

```text
level:    E1 Contract（类型/边界/命令）| E2 Deterministic（单测/性质/转移）
        | E3 Scenario（端到端确定性场景）| E4 Environment（真机/外部依赖）
evidence: E1/E2 = 可执行命令；E3/E4 = scenario/trace/runtime evidence 引用
```

CLOSED 必须附该声明的实际运行结果。「可复现」由等级声明保证，不退化
为描述。

## 5. 失败转移边（现补）

| 失败 | 转移 | 附加规则 |
|---|---|---|
| Review reject | → IMPLEMENT | 质量问题，改实现 |
| Verify：实现不满足 acceptance | → IMPLEMENT | 代码问题 |
| Verify：语义/假设失败 | → RESOLVE | **必须修订 Change State**（错误假设留痕，revision 而非静默改写） |
| Verify：harness/环境失败 | → VERIFY（留位重试） | **有界重试**；确认无解 → `BLOCKED` 记录（NO_REAL_BUYER 语义），不死循环 |

失败尝试本身是证据：保留，不删除。

## 6. Learning Hook（非状态，可选）

CLOSED 后仅在以下任一成立时触发（domain-modeling 三问式）：

```text
- 同类摩擦重复出现 ≥2 次（→ ADR 候选 / 流程校准）
- 新术语定型（→ CONTEXT.md 增补）
- 已有决策被证据推翻（→ ADR supersede）
```

燃料来源：friction 簿（人工，短期折衷）；Metrics 机械化延后至 Flow
Trace/Event 自动推导（依赖未来的 trace 设施，属 UNIFLOW_OPTIMIZATION）。

## 7. 延后项（有 buyer 再落）

| 项 | 触发条件 | 形态 |
|---|---|---|
| 并发 Change 协调 | 真实出现 2-3 个并发 Change | `depends_on` / `conflicts_with` 字段（**不建 DAG 系统**）+ worktree 隔离 |
| Metrics 机械化 | Flow Trace/Event 设施存在 | 从事件自动推导，不人工记 |

## 8. 其余章节

分层模型 / Skill 地图（三层）/ 上下文经济学（聪明区）/ ADR 关系
（0007 + 0003/0004 superseded）/ 视频洞见四入模点——**均沿用 v2 原文**
（§0/§5/§6/§7/§9 对应章节），无修改。

## 9. 实施清单（批准后单序列执行）

1. ADR-0007（含本版失败转移表与入口协议）+ ADR-0003/0004 superseded 头注
2. 重写 `.agents/skills/uniflow/SKILL.md`：Part A = 流程主干（8 状态 +
   失败边 + 入口协议 + 验证声明格式）；Part B = 执行引擎（不变项）
3. `changes/README.md`：状态机图 + 三档模板（MINIMAL=commit 约定）+
   verification 声明字段 + resume 复验清单 + BLOCKED 语义
4. 安装 `research` + `prototype`
5. CONTEXT.md 增补（Change State / 三档 / 状态 token / Smart Zone）
6. AGENTS.md 真相表 + schema enum 同步

## 10. 待批准（合并后的完整决策面）

a-e 沿用 v2 方案建议（changes/ 形态 / uniflow 单文件 / 立即装
research+prototype / ADR 头注 / dogfood 选题=本仓自改）+ 本版新增：
f) MINIMAL=commit 约定、g) 验证声明四等级、h) 失败边四条 + 两加固、
i) LEARN 触发三条件——**如无异议，回复「按方案执行」即全部生效**。
