# 方案 — Development Flow 实现基线 v2.2（APPROVED）

> PlanType: ARCHITECTURE_PLAN · Status: **APPROVED**（所有者 2026-09-07 裁决：
> 「v2.1 可批准，先修 4+1 点再执行；修完后足够作为实现基线，不再加结构」）
> 前版：v2.1（本版并入所有者 4+1 修正）

## 0. 最终流程形态（结构冻结）

```text
ENTER / RESUME（协议）
  ↓
UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT → REVIEW → VERIFY → CLOSED
                                                  ↖ failure edges ↙
                                                           ↓（可选）
                                                  LEARNING HOOK（钩子）
```

8 状态主干 = lifecycle；ENTER/RESUME = protocol；LEARN = optional hook；
`BLOCKED` = **正交 disposition**（非第 9 状态）。**结构到此冻结，后续仅经
LEARN hook 的证据演进。**

## 1. Entry / Resume Protocol

```text
1. 读 Change State（STANDARD+ 的 changes/<id>/state.md）
2. 复验（不信记录本身）：git 实况 vs 声明 base/进度；evidence 文件存在
   且匹配；status 与仓库一致
3. 一致 → 续行；任一矛盾 → 回 UNDERSTAND
```

## 2. 不确定性路由（AUTOMATIC FIRST）

六路由：人歧义→grill-with-docs；域概念→domain-modeling；外部→research；
经验→prototype；Bug 根因→diagnosing-bugs；无→直行 RESOLVED。
Resolve Gate 六检查；RESOLVED 为 semantic state。

## 3. 持久化（Durability ALWAYS, Depth VARIABLE）——修正 1

| 档 | 载体 | 约束 |
|---|---|---|
| STANDARD / DECISION-HEAVY | `changes/<id>/state.md` | 支持跨会话 in-flight resume |
| MINIMAL | **仅 commit message 约定** | **只允许用于无需跨会话恢复的 Change；一旦中断/跨 session → 自动升级 STANDARD 补建 state.md** |

三档字段沿用 v2；Change State = WHAT/WHY/ACCEPTANCE，非 Plan/Ticket/ADR。

## 4. 可复现验证声明——修正 2/3

等级命名（**避免与产品侧 E0-E4 证据分级混淆**）：

```text
CONTRACT（类型/边界/命令）· DETERMINISTIC（单测/性质/转移）
SCENARIO（端到端确定性场景）· ENVIRONMENT（真机/外部依赖）
```

**任何等级都必须包含四元组**（命令型可紧凑表达）：

```text
method/procedure · expected result · actual result · evidence ref
```

CLOSED 必须附四元组的实际运行结果；只有 trace 链接 ≠ 可复现验证。

## 5. 失败转移边——修正 4/5

| 失败 | 转移 | 附加规则 |
|---|---|---|
| Review：implementation defect | → IMPLEMENT | 质量问题 |
| Review：semantic / assumption defect | → RESOLVE | 修订 Change State（留痕 revision） |
| Verify：实现不满足 acceptance | → IMPLEMENT | — |
| Verify：语义/假设失败 | → RESOLVE | 同上，修订 Change State |
| Verify：harness/环境失败 | → 留在 VERIFY | 有界重试；无解 → **`disposition: BLOCKED`（lifecycle_state 仍为 VERIFY）**；恢复条件满足后继续 VERIFY |

失败尝试是证据：保留。`BLOCKED` 恢复条件记录于 state.md。

## 6. Learning Hook（可选，三触发）

摩擦 ≥2 次重复 / 新术语定型 / 决策被推翻 → 分别产出 ADR 候选、CONTEXT
增补、ADR supersede。燃料暂为 friction 簿；Metrics 机械化延后（Flow
Trace/Event 设施就绪后自动推导）。

## 7. 延后项（wait-for-buyer）

并发协调（`depends_on`/`conflicts_with`，非 DAG 系统）；Metrics 机械化。

## 8. 沿用章节（v2 原文有效）

Skill 三层地图 / 上下文经济学（聪明区 ≈150K 工作值）/ ADR 关系 /
视频洞见映射表。

## 9. 实施清单（本版批准后立即执行）

1. ADR-0007 + ADR-0003/0004 superseded 头注
2. 重写 `.agents/skills/uniflow/SKILL.md`（Part A 主干 / Part B 引擎）
3. `changes/README.md`（状态机 + 三档 + MINIMAL 升级规则 + 验证四元组 +
   BLOCKED disposition + resume 清单）
4. 安装 `research` + `prototype`
5. CONTEXT.md 增补（Change State / Smart Zone）
6. AGENTS.md 真相表 + schema enum 同步
