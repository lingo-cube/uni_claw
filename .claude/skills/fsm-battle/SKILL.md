---
name: fsm-battle
description: FSM 双轨对战 —— shadow-fsm-analyzer（需求驱动）vs fsm-analyzer（源码驱动）并行分析同一问题，brainstorming 对质，蒸馏共识知识。用于 FSM 诊断、矩阵审计、handler 审查、回归分析。
---

# FSM Battle Skill

让 shadow-fsm-analyzer（需求优先，独立设计）与 fsm-analyzer（源码优先，实现审查）从正交视角分析同一 FSM 问题，然后对质、brainstorming、蒸馏知识。

## 什么时候使用

- FSM 行为异常诊断（"为什么 run 卡在 ErrorHandling 循环？"）
- 转移矩阵 / handler 逻辑审查（"这个 handler 的决策表是否完备？"）
- 回归分析（"上次修复后，FSM 行为是否与需求一致？"）
- 知识蒸馏（"当前对 FSM 的理解有哪些盲区？"）
- 重构验证（"矩阵加固设计是否正确？源码落地了吗？"）

## 前置条件

1. shadow-fsm-analyzer agent 已创建且 memory 目录存在
2. fsm-analyzer agent 已创建且 memory 目录存在
3. 有明确的 battle 主题（一个 FSM 问题 / handler / run / 矩阵区域）

## 执行流程

### Phase 1: 并行分析（fan-out）

启动两个子代理并行分析同一问题。

**shadow-fsm-analyzer**（haiku，需求→测试→设计）:
```
Agent(subagent_type="shadow-fsm-analyzer", model="haiku")
prompt: 分析目标 + 约束（绝不读 C# FSM 源码）+ 输出要求
```

**fsm-analyzer**（haiku，源码→矩阵→诊断）:
```
Agent(subagent_type="fsm-analyzer", model="haiku")
prompt: 分析目标 + 输出要求（源码锚定行号）
```

**并行启动**：两个 agent 在同一条消息中同时启动，互不阻塞。

等待双方完成。收集各自的输出。

### Phase 2: 差异提取

不需启动新 agent。由主对话直接比对两份输出：

1. **提取 FSM 模型描述**——从双方输出中提取可比较的 FSM 模型（状态 / 转移 / handler 决策 / 门限值）
2. **交叉比对**：
   - 共识点（双方独立收敛到相同结论）→ 高置信度知识
   - 争议点（双方结论不一致）→ 需要 brainstorming 对质的调查目标
3. **分类差异**：
   - 需求歧义（双方都对但角度不同）
   - 实现偏差（源码做了但需求没说要做的）
   - 需求未覆盖（需求说了但源码没实现）
   - 测试盲区（双方都无法从各自信息源确认）
   - Shadow 推断错误（shadow 从需求/测试推断错了）
   - 文档滞后（spec/charter 与 patterns/源码不一致）

### Phase 3: Brainstorming 对质

对每个争议点，启动一轮对质。**两种模式**：

#### 模式 A: 串行对质（争议点 ≤3 个）
对每个争议点，先让一方追问另一方，然后反过来。
```
→ Agent(shadow, "fsm-analyzer 说 X，你的看法是？")
→ Agent(fsm-analyzer, "shadow 说 Y，从源码验证")
→ 主对话合成结论
```

#### 模式 B: 批量对质（争议点 >3 个）
把所有争议点打包成结构化问卷，同时发给双方。
```
→ Agent(shadow, "以下是 fsm-analyzer 的争议点列表：[...]。逐条回应：同意/不同意/需要更多证据")
→ Agent(fsm-analyzer, "以下是 shadow 的争议点列表：[...]。逐条回应并用源码行号验证")
→ 主对话合成结论
```

### Phase 4: 知识蒸馏

Battle 结束后，由主对话执行：

1. **写入 battle-log.md**（shadow 侧）
2. **更新 shadow fsm-design.md**（如有新发现改变了设计）
3. **更新 shadow knowledge.md + lessons.md**
4. **更新 fsm-analyzer knowledge.md + lessons.md**（通过 Agent 工具发送更新指令）
5. **输出 battle 摘要报告**给用户，包含：
   - 共识点清单（可沉淀到双方知识库）
   - 已解决的争议点 + 解决方式
   - 持久争议点（需要更多证据 / 人类裁决）
   - 行动建议（补测试 / 修文档 / 改源码 / 澄清需求）

### Phase 5: 人类裁决（Gate）

用户审查 battle 摘要报告，决定：
- 哪些共识点需要写入长期记忆
- 持久争议点如何裁决
- 是否需要补更多证据（如运行时 trace）再战一轮

**用户裁决前，不修改源码。**

## Battle 主题示例

### 主题 1: Handler 决策表审查
```
/fsm-battle handler=HandleErrorHandlingAsync
```
Shadow 从测试+需求推导决策表；fsm-analyzer 从源码提取决策表；比对差异。

### 主题 2: 转移矩阵审计
```
/fsm-battle matrix
```
Shadow 从 patterns+constitution+测试推断转移矩阵；fsm-analyzer 从 TransitionMatrix 字段提取矩阵；比对差异。

### 主题 3: Run 诊断
```
/fsm-battle run=artifacts/runs/20260805T123529899Z
```
Shadow 从 run.log+trace+需求推断应该发生什么；fsm-analyzer 从源码+run.log 诊断实际发生了什么；比对差异。

### 主题 4: 特定问题
```
/fsm-battle topic="为什么 FrameComplete 在 enumerate 场景中从未触发？"
```
双方独立分析，各自动用各自的工具链，最后对质。

## 输出格式

Battle 摘要报告格式：

```
═══════════════════════════════════════════
FSM Battle Report — <theme> — <date>
═══════════════════════════════════════════

## Participants
- shadow-fsm-analyzer: [需求+测试驱动, haiku]
- fsm-analyzer: [源码驱动, haiku]
- Battle topic: <topic>
- Rounds: N

## Consensus (HIGH confidence)
- <point 1> — both independently converged
- <point 2> — both independently converged

## Resolved Disputes
| Dispute | Shadow | FSM-analyzer | Resolution | Action |
|---------|--------|-------------|------------|--------|
| <dispute 1> | <shadow position> | <fsm-analyzer position> | <how resolved> | <what to do> |

## Unresolved Disputes (needs human)
| Dispute | Shadow | FSM-analyzer | Why unresolved |
|---------|--------|-------------|----------------|
| <dispute> | <position> | <position> | <reason> |

## Knowledge Distilled
- Shadow knowledge base: <changes made>
- FSM-analyzer knowledge base: <changes made>
- Battle log: <path>

## Recommended Actions
1. <action 1>
2. <action 2>
```

## 硬约束

1. **Shadow 绝不读 C# FSM 源码**——battle 的价值在于正交视角，违反此约束 battle 就失去了意义
2. **FSM-analyzer 绝不读 shadow 的 fsm-design.md**——它只能从源码出发
3. **Haiku 优先**——两个分析 agent 都用 haiku 模型（token 成本低，适合机械性分析）；brainstorming 对质回合也用 haiku；只有最终合成和人类对话用主模型
4. **Battle 不修改源码**——只输出分析结论和建议；源码修改必须经过人类裁决
5. **双方都可委托 trace-analyzer**——当需要运行时 trace 证据时
6. **Battle log 写入 shadow 侧**——`shadow-fsm-analyzer-memory/battle-log.md`
7. **Memory 更新双向**——battle 结论如果有价值，同时更新双方的知识库

## 相关 Agent

- [shadow-fsm-analyzer](../../.claude/agents/shadow-fsm-analyzer.md) — 需求驱动，独立 FSM 设计
- [fsm-analyzer](../../.claude/agents/fsm-analyzer.md) — 源码驱动，FSM 诊断优化
- [trace-analyzer](../../.claude/agents/trace-analyzer.md) — 运行时 trace 证据（双方共享委托目标）
