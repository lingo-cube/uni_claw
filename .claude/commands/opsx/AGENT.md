# OpenSpec Agent 自动调度规范

> 本文件是 `/opsx:propose` 与 `/opsx:apply` 的编排规则单点真源。
> 命令文件头部以 `Read .claude/commands/opsx/AGENT.md` 引用本文件，触发时自动载入。
> 改规则只改这里，不动命令文件。

## 触发规则

执行 `/opsx:propose` 或 `/opsx:apply` 时，立即启用**【Fable 编排模式】**。

> `/opsx:explore` **不启用编排模式**。它保持思考姿态，可选派 `openspec-researcher`（haiku/只读）做检索归纳（规则见 `explore.md` § Lightweight Retrieval & Synthesis），但不进 Fable/Opus 统筹链路，不派 coder/refactorer。

> Fable = glm-5.2[1M]（最高阶统筹模型）。主对话会话模型即 Fable，
> **顶层统筹 = 主循环本身**，不再额外开一个 "orchestrator 子代理"。

## 1. 编排层规则（顶层决策）

- 执行 OpenSpec 流程，优先以 **Fable** 作为主协调器。
- 若当前会话模型不是 Fable，优先尝试启用 Fable 统筹。

### 🔁 降级规则

**当 Fable 模型不可用、未配置、调用失败时，顶层统筹自动降级为 Opus，不再继续降级。**

- 降级链路：`Fable → Opus`（止步，禁止继续落到 Sonnet / Haiku 承担顶层架构规划）
- 降级时显式提示：
  > 【统筹模型降级通知】Fable 不可用，自动切换至 Opus 承担顶层任务规划与评审。
- Opus 也异常时，向用户告警，**不继续自动降级**，等待人工介入切换模型。

**顶层统筹角色职责**：
需求边界校验、架构决策、复杂逻辑推演、多子任务成果合并、风险识别、代码一致性审查。

## 2. 子 Agent 分层派发规则（顶层统筹负责调度）

顶层统筹规划完成后，主动拆分任务并指派对应档位 SubAgent。
**SubAgent 类型与档位绑定**（model 枚举背后由代理层路由到对应模型）：

| 子任务类型 | Agent 类型 (`subagent_type`) | `model` 档位 | 背后模型 |
|---|---|---|---|
| 文件检索、日志解析、正则校验信息探查（轻量只读） | `openspec-researcher` | `haiku` | deepseek-v4-flash |
| 常规功能编码、普通 Bug 修复、单元测试、接口实现 | `openspec-coder` | `sonnet` | deepseek-v4-pro |
| 跨模块重构、复杂流程梳理、深度故障定位 | `openspec-refactorer` | `opus` | deepseek-v4-pro |

派发用 **Agent 工具**：`Agent(description=..., prompt=<含上下文与任务边界>, subagent_type=openspec-coder)`。
独立子任务在**单条消息内并发派发**多个 Agent 调用。

## 3. 任务判定标准

满足下面**任意条件**，必须启用多 SubAgent 并行执行：
- 改动 ≥ 3 个代码文件
- 涉及架构调整、新增模块、状态机/并发逻辑
- 需求存在歧义、需要多方案对比
- 包含「方案设计 + 编码落地 + 验证自测」完整链路

**简单单行修改、常量调整、日志打印、语法修复**：禁止派生 SubAgent，顶层统筹直接完成。

## 硬性约束

1. **只有顶层统筹角色允许使用 Fable**。
   SubAgent 子任务**禁止调用 Fable**，子任务上限为 Opus 档位（`openspec-refactorer`）。
2. **顶层统筹只负责规划、评审、难点攻坚**；大量机械编码委派下级 Agent，控制旗舰 token 消耗。
3. **所有子任务完成后**，由顶层统筹统一校验：代码一致性、逻辑冲突、边界缺陷。
4. **火山级 / 宪章级决策上抛用户**：新增 enum 值、改约束、改 layer 拓扑等，子代理不得自行实施，必须回报顶层统筹由其向用户确认（与 CLAUDE.md「如有新增要寻得用户同意」一致）。
5. **C# 代码查询 MCP 优先**：子代理与顶层统筹查询 C# 符号（定义/引用/继承/诊断）时，**先 MCP 定位再 Read 片段，禁止 grep/find 定位 C# 符号**。详见 `.claude/MCP-QUERY.md`。

## Slash 命令联动

- `/opsx:propose` → 启动 Fable 优先编排工作流（Fable 失效自动降级 Opus）。统筹规划后，按需派 researcher/coder/refactorer 生成 proposal/design/tasks/specs。
- `/opsx:apply` → 在顶层统筹生成的规范方案基础上执行落地。按 tasks 拆分，机械编码派 coder，跨模块任务派 refactorer，全程统筹校验。

## 降级兜底策略（汇总）

1. 优先 Fable；
2. Fable 不可用 → 顶层使用 Opus；
3. Opus 也异常 → 向用户告警，不继续自动降级，等待人工介入切换模型。