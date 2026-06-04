# uni-claw 车机菜单遍历测试引擎 - PRD V4.0（图·状态机·Trace·AI 综合设计）

> **文档版本**: V4.0  
> **最后更新**: 2025-06-01  
> **状态**: 规划中（下一阶段全面升级）  
> **依赖**: V3.0 已实现基础遍历引擎、状态管理、异常处理链、按钮类型区分  
> **核心新增**: 图模型、三层状态机、全链路 Trace、AIProvider 与安全策略（含元素预筛）、截图存储

---

## 1. 产品概述与目标

### 1.1 定位
**uni-claw V4.0** 在已验证的线性遍历基础上，升级为**图驱动的智能遍历引擎**，实现：
- **可规划的图模型**：静态节点 + 动态模板，覆盖精确路径与自动探索。
- **严谨的状态机控制**：三层状态（全局生命周期、遍历循环、深度优先栈），支持断点续跑与复杂回退。
- **可观测的 Trace**：全链路记录每一步决策、执行与状态变更，支持回放与 AI 质量评估。
- **AI 增强与安全**：四个能力的 AIProvider，从元素预筛、页面验证到异常兜底决策，全程绑定安全策略，实现零破坏性操作。

### 1.2 核心价值
- **统一节点模型**：静态图、动态匹配、AI 决策输出均使用 `TraversalNode`，执行层无差别处理。
- **规则为主，AI 为辅**：90% 决策由规则引擎完成，AI 仅在模糊、异常时介入，且所有输出强制安全校验。
- **全链路可回放**：Trace 记录完整的决策与执行上下文，支持严格、决策、模拟三种回放模式。
- **渐进式升级**：通过配置开关启用图模式，保留现有 V3.0 线性模式，平滑过渡。

---

## 2. 系统架构

```
                        ┌──────────────────────────────────────────┐
                        │           输入层                          │
                        │  自然语言指令 / 静态DAG / 断点恢复          │
                        └──────────────────┬───────────────────────┘
                                           ▼
                        ┌──────────────────────────────────────────┐
                        │         计划层 (TraversalPlan)            │
                        │  - 根节点 (TraversalNode)                 │
                        │  - 静态节点图 (可选)                       │
                        │  - 模板注册表引用                          │
                        └──────────────────┬───────────────────────┘
                                           ▼
                        ┌──────────────────────────────────────────┐
                        │                执行层                     │
                        │  ┌─────────────────────────────────────┐ │
                        │  │       三层状态机                     │ │
                        │  │  - 全局状态机 (生命周期)              │ │
                        │  │  - 遍历状态机 (单节点循环)            │ │
                        │  │  - 节点栈 (深度优先路径)              │ │
                        │  └─────────────────────────────────────┘ │
                        │  ┌─────────────────────────────────────┐ │
                        │  │        AIProvider (可选)             │ │
                        │  │  能力1: 自然语言→计划                │ │
                        │  │  能力2: 页面类型验证                 │ │
                        │  │  能力3: 元素安全预筛                 │ │
                        │  │  能力4: 上下文决策(异常/分支)        │ │
                        │  │  ├─ 内嵌 SafetyPolicy               │ │
                        │  └─────────────────────────────────────┘ │
                        │  ┌─────────────────────────────────────┐ │
                        │  │        安全过滤器 (全局)             │ │
                        │  │  执行前最终校验                       │ │
                        │  └─────────────────────────────────────┘ │
                        └──────────────────┬───────────────────────┘
                                           ▼
                        ┌──────────────────────────────────────────┐
                        │            基础设施层                      │
                        │  - 视觉分析模型 (已有)                     │
                        │  - 截图存储 (本地/OSS)                    │
                        │  - Trace 记录器                           │
                        └──────────────────┬───────────────────────┘
                                           ▼
                        ┌──────────────────────────────────────────┐
                        │            输出与观测                     │
                        │  - 遍历结果树 (ContentTree)               │
                        │  - 遍历报告                               │
                        │  - Trace 文件 (JSONL + 截图)              │
                        │  - AI 决策审计日志                        │
                        └──────────────────────────────────────────┘
```

---

## 3. 图模型详细设计

### 3.1 核心思想
遍历任务抽象为**有向无环图 (DAG)**，节点表示操作，边由 `children_strategy` 定义生成规则。支持：
- **静态图**：预定义节点及子节点列表，用于精确控制关键路径。
- **动态图**：基于模板注册表和屏幕元素实时生成子节点，用于自动探索未知菜单。
- **混合模式**：静态节点中未定义的部分自动切换为动态探索。

### 3.2 统一节点结构：TraversalNode

| 字段 | 类型 | 说明 |
|------|------|------|
| `node_id` | `str` | 唯一标识 |
| `name` | `str` | 显示名（用于日志） |
| `node_type` | `str` | 节点类型：`container`, `leaf_switch`, `leaf_slider`, `leaf_action`, `leaf_info` |
| `operation` | `Operation` | 具体动作定义 |
| `precondition` | `Optional[Precondition]` | 执行前需满足的页面状态 |
| `children_strategy` | `ChildrenStrategy` | 子节点生成策略 |
| `error_policy` | `Optional[ErrorPolicy]` | 节点级异常处理（覆盖全局） |
| `meta` | `dict` | 运行时状态 (`visited`, `retry_count` 等) |

**Operation**
| 字段 | 类型 | 说明 |
|------|------|------|
| `action` | `str` | `click`, `swipe`, `back`, `input_text`, `no_action` |
| `target` | `Optional[Target]` | 定位方式 (`by`: `text`/`coordinate`/`ui_index`; `value`: 对应值) |
| `params` | `dict` | 动作参数 (如滑动目标值) |
| `restore` | `Optional[RestoreAction]` | 恢复操作 (如开关反向点击) |

**Precondition**
| 字段 | 类型 | 说明 |
|------|------|------|
| `page_name` | `Optional[str]` | 要求页面名称匹配 `current_path[-1]` |
| `path` | `Optional[List[str]]` | 要求完整路径匹配 |
| `ui_condition` | `Optional[str]` | 自定义条件，如 `"screen_contains('亮度')"` |

**ChildrenStrategy**
| 字段 | 类型 | 说明 |
|------|------|------|
| `type` | `StrategyType` | `STATIC` / `DYNAMIC_MATCH` / `NONE` |
| `static_children` | `Optional[List[str]]` | 静态子节点ID列表 (STATIC 时必填) |
| `dynamic_rules` | `Optional[Dict[str, DynamicRule]]` | 动态匹配规则集 (DYNAMIC_MATCH 时必填) |

**DynamicRule**
| 字段 | 类型 | 说明 |
|------|------|------|
| `match_condition` | `dict` | 匹配条件，基于 `MenuItem` 字段，如 `{"type": "menu_item", "expected_action": "navigate"}` |
| `child_template` | `str` | 匹配后使用的模板ID (注册表中) |
| `action` | `str` | 匹配动作: `generate_child` / `skip` / `execute_inline` |

### 3.3 模板注册表

可配置 JSON 文件，定义通用控件行为。示例：
```json
{
  "menu_container": {
    "node_type": "container",
    "operation": {"action": "click", "target": {"by": "text", "value": "{{item_text}}"}},
    "precondition": {"ui_condition": "screen_contains('{{item_text}}')"},
    "children_strategy": {
      "type": "dynamic_match",
      "dynamic_rules": {
        "menu_rule": {
          "match_condition": {"type": "menu_item", "expected_action": "navigate"},
          "child_template": "menu_container",
          "action": "generate_child"
        },
        "switch_rule": {
          "match_condition": {"type": "switch"},
          "child_template": "switch_leaf",
          "action": "generate_child"
        }
      }
    }
  },
  "switch_leaf": {
    "node_type": "leaf_switch",
    "operation": {
      "action": "click", "target": {"by": "text", "value": "{{item_text}}"},
      "restore": {"needed": true, "action": "click", "target": {"by": "text", "value": "{{item_text}}"}}
    },
    "children_strategy": {"type": "none"}
  }
}
```
模板中的 `{{item_text}}` 在运行时由实际元素文本替换。

---

## 4. 三层状态机设计

### 4.1 全局状态机
管理遍历任务生命周期，状态与转换：
```
IDLE → INITIALIZING → TRAVERSING ⇄ PAUSED
                    ↘ ERROR → RECOVERING → TRAVERSING
                              ↘ TERMINATED
                         TRAVERSING → COMPLETED
```
- `IDLE`: 等待任务
- `INITIALIZING`: 加载计划、初始化上下文、定位入口应用
- `TRAVERSING`: 核心遍历循环运行中
- `PAUSED`: 外部暂停（可保存状态）
- `ERROR`: 发生严重错误（设备离线、APP崩溃等）
- `RECOVERING`: 执行恢复流程（重启APP、重连ADB等）
- `COMPLETED`: 遍历正常结束
- `TERMINATED`: 不可恢复终止

### 4.2 遍历状态机（单节点循环）
在全局 `TRAVERSING` 下激活，状态：
```
NODE_SELECT → PRECONDITION_CHECK → EXECUTE → RESULT_VERIFY → BRANCH
     ↑                                                          │
     └──────────────────────────────────────────────────────────┘
```
- `NODE_SELECT`: 从节点栈顶部获取当前帧，选择下一个待处理的子节点（来自 `child_queue`）。
- `PRECONDITION_CHECK`: 检查当前屏幕是否满足节点的 `precondition`。不满足则自动执行返回/等待直到满足或超时。
- `EXECUTE`: 执行节点的 `operation`，包含安全过滤器检查。
- `RESULT_VERIFY`: 截图并分析，判断操作结果（成功、弹窗、跳转、无反馈、异常）。
- `BRANCH`: 根据结果分支：
  - 容器节点进入新页面 → 生成子节点并压栈。
  - 叶子节点且需恢复 → 执行 `restore`，然后标记完成。
  - 当前帧所有子节点完成 → 执行返回并从栈中弹出。
  - 异常 → 触发节点 `error_policy` 或全局异常链。

### 4.3 节点栈
维护深度优先遍历路径，每个帧 `StackFrame`：
```python
@dataclass
class StackFrame:
    node: TraversalNode            # 当前节点
    child_queue: List[str]         # 待处理的子节点ID列表（按序）
    current_child_idx: int         # 当前子节点索引
    pending_restore: bool          # 出栈时是否需要执行恢复操作
```
操作：
- `push(node, children_ids)`: 进入新节点。
- `top()`: 获取当前帧。
- `pop()`: 子节点均完成且恢复操作已执行后出栈。

### 4.4 与图模型的交互
- `NODE_SELECT` 阶段检查 `children_strategy`：
  - `STATIC`：直接从静态列表初始化 `child_queue`。
  - `DYNAMIC_MATCH`：执行容器的 `operation` 后，获取新页面 `PageAnalysis`，对每个 `MenuItem` 应用 `dynamic_rules` 生成子节点并压入栈。
- 当所有子节点处理完毕，`BRANCH` 执行 `pop`，必要时执行恢复操作。

---

## 5. Trace 系统设计

### 5.1 记录内容
每次遍历生成一个 `TraversalTrace`，包含：
- `session_info`: 设备、应用、时间、模式等。
- `steps: List[TraceStep]`: 每一步详细记录。
- `state_snapshots`: 定期快照（每10步），用于快速恢复。
- `ai_interactions: List[AIInteraction]`: AI 调用详情。
- `summary`: 统计摘要。

**TraceStep 核心字段**:
| 字段 | 说明 |
|------|------|
| `step_id` | 自增序号 |
| `timestamp` | 时间戳 |
| `global_state` | 全局状态 |
| `traversal_state` | 遍历状态 |
| `page_summary` | 屏幕摘要 (路径, 元素数量, 弹窗) |
| `decision` | 决策详情 (节点ID, 类型, 操作, 来源: RULE/AI/SAFETY) |
| `execution` | 执行结果 (成功/失败, 耗时, 截图引用) |
| `stack_trace` | 当前节点栈ID列表 |
| `path_before/after` | 操作前后 `current_path` |

**AIInteraction**:
| 字段 | 说明 |
|------|------|
| `method` | 调用方法名 (`screen_elements`, `make_decision` 等) |
| `input_summary` | 输入的页面摘要与上下文摘要 |
| `output_raw` | AI 原始响应 |
| `was_accepted` | 是否被采纳 |
| `safety_blocked` | 是否被安全策略拦截 |
| `execution_result` | 若执行，结果如何 |

### 5.2 存储格式
- JSON Lines 文件 (`trace.jsonl`)，每条一个步骤或 AI 交互。
- 截图单独存储（本地或OSS），Trace 中只存引用。
- 每个遍历任务一个文件夹，含 `trace.jsonl`, `ai_interactions.jsonl`, `screenshots/`, `summary.json`。

### 5.3 回放能力
- **严格回放**：按记录顺序重放操作，验证 UI 响应一致性。
- **决策回放**：复用决策序列（节点图），重新执行操作，允许微小 UI 差异。
- **模拟回放**：离线分析 Trace 数据，计算覆盖率、AI 准确率等。

### 5.4 与状态机的集成
- 状态机在每次状态转换时调用 `TraceRecorder` 钩子。
- `EXECUTE` 前后记录步骤，`BRANCH` 中记录分支决策。
- 异常发生时，记录异常上下文并关联到步骤。
- 所有 AI 调用均通过装饰器或包装器记录完整的输入输出。

### 5.5 Trace 对状态机的影响
- **可恢复性**：状态机崩溃或暂停后，可从最近的 `StateSnapshot` 重建 `node_stack` 和 `visited_pages`，继续遍历。
- **性能开销**：Trace 写入采用异步缓冲，避免阻塞主循环。
- **调试增强**：当状态机行为异常时，可加载 Trace 在模拟回放模式下逐步分析。

---

## 6. AIProvider 与安全策略

### 6.1 定位
AIProvider 是**可选智能增强层**，不处理原始图像，仅基于结构化 `PageAnalysis` 和上下文推理。默认实现 `NoOpAIProvider` 保证系统在无 AI 时正常运行。

### 6.2 四项核心能力

#### 能力1: 自然语言→遍历计划 (`parse_task_to_plan`)
- 输入: 自然语言指令字符串
- 输出: `TraversalPlan` (包含根节点、可选静态节点、注册表引用)
- 用于从用户描述自动构建初始图模型。若失败，回退为纯动态计划。

#### 能力2: 页面类型验证 (`verify_page_type`)
- 输入: `PageAnalysis` + `PageExpectation` (预期页面名、必需元素等)
- 输出: `TypeCheckResult` (是否匹配、置信度、实际类型、修复建议)
- 辅助 `precondition` 检查，当视觉模型判断模糊时提供参考。

#### 能力3: 元素安全预筛 (`screen_elements`)
- **第零层防护**：在视觉分析之后、状态机决策前运行。
- 输入: 待判断的 `MenuItem` 列表（通常来自规则初筛后的模糊项）、当前页面、上下文
- 输出: 修改每个元素的 `safety_tag` 字段 (`safe` / `caution` / `skip` / `unknown`)
- AI 批量判断模糊元素的语义安全性，规则扫描已过滤明显危险项。
- 通过 `safety_tag` 影响状态机 `NODE_SELECT`：`skip` 直接跳过，`caution` 降低优先级或仅记录不点击。

#### 能力4: 上下文决策 (`make_decision`)
- 输入: `DecisionContext` (触发原因、UI分析、遍历上下文)
- 输出: `(DecisionResult, Optional[TraversalNode])`
- 用于异常兜底（当责任链无法处理）、分支选择不明确、寻找特定目标时。
- 返回的节点必须经过 `SafetyPolicy` 校验。

### 6.3 SafetyPolicy 绑定
- AIProvider 构造函数强制注入 `SafetyPolicy`，无则报错。
- 所有生成 `TraversalNode` 的方法在返回前调用 `self._safety.validate(node, context)`。
- 不安全节点被替换为安全回退 (`fallback_node`) 或导致返回 `UNSURE`。

### 6.4 四层安全防护总结

| 层级 | 位置 | 时机 | 职责 |
|------|------|------|------|
| **第零层** | 元素预筛 (规则+AI) | 截图分析后，决策前 | 标记危险元素，状态机避开 |
| **第一层** | AIProvider 内部 | AI 生成节点时 | 过滤 AI 输出的危险操作 |
| **第二层** | 全局 SafetyFilter | 任何节点执行前 | 最终兜底，防止配置错误或绕过 |
| **第三层** | 设备驱动层 | 执行操作时 | 系统级保护（如 ADB 权限） |

### 6.5 安全策略实现要点
- 危险文本黑名单、操作白名单、坐标越界检查等通过责任链规则实现。
- 安全策略可通过 `SafetyConfig` 定制（如允许 `input_text` 需显式授权）。
- 所有拦截均记录审计日志，并可触发告警。

---

## 7. 组件间相互影响分析

### 7.1 Trace 与状态机
- **Trace 依赖状态机**：状态机提供所有状态转换和决策事件，Trace 作为观察者记录。
- **状态机恢复依赖 Trace**：断点恢复时，状态机从 `StateSnapshot` 重建 `node_stack` 和全局状态。
- **性能影响**：Trace 写入异步进行，状态机主循环不等待 I/O，确保遍历效率。

### 7.2 AIProvider 与状态机
- **元素预筛影响 NODE_SELECT**：带有 `safety_tag=skip` 的元素不会被放入待处理队列。
- **页面验证影响 PRECONDITION_CHECK**：当视觉模型返回路径模糊时，AI 验证结果可辅助判断是否满足条件。
- **决策影响 BRANCH**：异常或分支不明确时，AI 输出节点直接进入执行流程。
- **安全策略影响 EXECUTE**：AI 生成的节点在执行前需通过全局 SafetyFilter，形成双重保险。

### 7.3 AIProvider 与 Trace
- **AI 调用被 Trace 记录**：每次 AI 请求/响应、耗时、采纳情况均被记录，用于离线分析和 Prompt 优化。
- **Trace 数据优化 AI**：通过分析 `AIInteraction` 中的失败案例、低置信度决策，可自动或半自动优化 Prompt，形成闭环。

### 7.4 图模型与状态机
- 图模型提供静态或动态的节点树，状态机负责按深度优先执行。
- 动态图中，状态机在执行容器操作后，调用图模型匹配逻辑生成子节点。
- 节点栈中的 `StackFrame` 直接持有 `TraversalNode` 实例，确保图与执行同步。

---

## 8. 截图存储方案

- 接口: `ScreenshotStorage` (save/load/delete/exists)
- 实现: `LocalScreenshotStorage` (本地目录) 和 `OSSScreenshotStorage` (阿里云OSS等)
- 配置: 通过 `ScreenshotStorageConfig` 选择 provider 和参数。
- 与 Trace 集成: Trace 步骤中保存 `screenshot_ref`，回放时通过存储接口加载。

---

## 9. 实施路线图

| 阶段 | 内容 | 预计周期 |
|------|------|----------|
| Phase 1: 图模型与模板引擎 | `TraversalNode` 数据结构、模板注册表、动态匹配逻辑 | 2周 |
| Phase 2: 三层状态机 | 全局/遍历/栈状态机，与图模型联调，兼容V3开关 | 2周 |
| Phase 3: Trace 系统 | 记录器、三种回放模式、与状态机和截图存储集成 | 1.5周 |
| Phase 4: AIProvider 基础 | 接口定义、`NoOpAIProvider`、安全策略、元素预筛规则 | 1.5周 |
| Phase 5: AI 对接与 Prompt | `RealAIProvider`、四项能力 Prompt、LLM 集成、A/B测试框架 | 2周 |
| Phase 6: 集成测试与文档 | 全场景测试、异常恢复测试、Trace 回放验证、使用手册 | 1周 |

---

## 10. 成功指标

- 图模型+状态机覆盖 5+ 款车机设置菜单，覆盖率 ≥95%。
- 新增控件类型只需修改注册表 JSON，无需代码变更。
- Trace 严格回放操作一致性 ≥90%。
- AI 生成节点 100% 通过安全策略，破坏性操作零执行。
- 元素预筛正确识别并跳过 ≥95% 的危险按钮。
- 异常自动恢复率（含 AI 兜底）≥85%。

---

*本 PRD 融合了图模型、状态机、Trace、AI 增强与安全策略的完整设计，各模块相互协同、层次分明，可作为下一阶段开发的权威蓝图。*