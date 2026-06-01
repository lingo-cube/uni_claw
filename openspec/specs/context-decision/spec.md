## ADDED Requirements

### Requirement: 上下文决策能力
系统 SHALL 提供 `ContextDecisionCapability` 能力，在遍历过程中做出下一步动作决策。

#### Scenario: 成功决策
- **WHEN** 可以确定下一步操作
- **THEN** 返回 `ContextDecisionResult` 包含：
  - `result`: "success"
  - `action`: 具体动作
  - `target`: 操作目标（可选）
  - `params`: 动作参数（可选）
  - `reasoning`: 决策理由
  - `confidence`: 0.7-1.0

#### Scenario: 决策不确定
- **WHEN** AI 无法确定下一步操作
- **THEN** 返回 `result` 为 "unsure"
- **AND** `confidence` 低于 0.7

#### Scenario: 放弃决策
- **WHEN** AI 判断无法达成目标
- **THEN** 返回 `result` 为 "give_up"

#### Scenario: 等待状态
- **WHEN** 需要等待页面加载或状态变化
- **THEN** 返回 `result` 为 "wait"
- **AND** `action` 为 "wait"

#### Scenario: 安全模式
- **WHEN** 安全筛选失败或检测到危险
- **THEN** 返回 `result` 为 "safe_mode"
- **AND** `action` 为 "back"

### Requirement: 支持的动作类型
系统 SHALL 支持多种动作类型用于遍历。

#### Scenario: 点击动作
- **WHEN** 需要点击元素
- **THEN** `action` 为 "click"
- **AND** `target` 包含定位方式和值

#### Scenario: 返回动作
- **WHEN** 需要返回上一级
- **THEN** `action` 为 "back"
- **AND** `target` 为 null

#### Scenario: 滑动动作
- **WHEN** 需要滑动屏幕
- **THEN** `action` 为 "swipe"
- **AND** `params` 包含滑动方向和距离

#### Scenario: 滚动动作
- **WHEN** 需要向下滚动列表
- **THEN** `action` 为 "scroll_down"

#### Scenario: 跳过动作
- **WHEN** 跳过当前目标
- **THEN** `action` 为 "skip"

#### Scenario: 无动作
- **WHEN** 不需要执行任何操作
- **THEN** `action` 为 "no_action"

### Requirement: 目标定位方式
系统 SHALL 支持多种目标定位方式。

#### Scenario: 文本定位
- **WHEN** 元素有可识别的文本
- **THEN** `target.by` 为 "text"
- **AND** `target.value` 为元素文本

#### Scenario: 坐标定位
- **WHEN** 元素无可识别文本
- **THEN** `target.by` 为 "coordinate"
- **AND** `target.value` 为 [x, y] 坐标数组

#### Scenario: 无目标
- **WHEN** 动作不需要目标（如 back）
- **THEN** `target` 为 null

### Requirement: 安全约束遵守
系统 SHALL 严格遵循安全筛选结果的约束。

#### Scenario: 禁止点击 skip 元素
- **WHEN** 元素被标记为 skip
- **THEN** 决策不包含点击该元素的操作

#### Scenario: 谨慎操作 caution 元素
- **WHEN** 存在 caution 元素
- **THEN** 优先选择 safe 元素
- **AND** 仅在无 safe 元素时考虑 caution

#### Scenario: 无 safe 元素返回
- **WHEN** 当前页面无 safe 元素
- **THEN** 决策执行 back 返回上一级

#### Scenario: 安全验证标志
- **WHEN** 返回决策结果
- **THEN** `safety_verified` 字段指示是否经过安全验证
- **AND** 为 true 时表示决策遵守安全约束

### Requirement: 弹窗处理
系统 SHALL 优先处理弹窗和对话框。

#### Scenario: 优先点击取消
- **WHEN** 检测到弹窗且包含取消按钮
- **THEN** 决策点击取消按钮
- **AND** `reasoning` 说明优先取消的原因

#### Scenario: 点击关闭按钮
- **WHEN** 弹窗有关闭按钮（X 图标）
- **THEN** 决策点击关闭按钮

#### Scenario: 点击弹窗外部
- **WHEN** 弹窗无明显关闭按钮
- **THEN** 决策点击弹窗外部区域（使用坐标）

#### Scenario: 执行返回
- **WHEN** 无法关闭弹窗
- **THEN** 决策执行 back 操作

### Requirement: 异常恢复
系统 SHALL 处理遍历过程中的异常情况。

#### Scenario: 元素未找到
- **WHEN** 目标元素未找到
- **THEN** 先尝试 back 返回并重试
- **AND** 若仍失败则 skip

#### Scenario: 点击无响应
- **WHEN** 点击操作无响应
- **THEN** 尝试点击同一坐标偏移 5% 的位置
- **AND** 或等待后重试

#### Scenario: 页面跳转异常
- **WHEN** 页面跳转异常
- **THEN** 连续 back 直到回到已知页面
- **AND** 检查 `visited_pages` 判断已知页面

#### Scenario: 连续失败处理
- **WHEN** 连续失败 3 次以上
- **THEN** 建议 back 到根页面并 skip 当前分支

### Requirement: 分支选择策略
系统 SHALL 根据遍历上下文选择合适的分支。

#### Scenario: 优先未访问分支
- **WHEN** 存在多个 menu_item
- **THEN** 优先选择未被访问的

#### Scenario: 避开危险元素
- **WHEN** 存在标记为 skip 或 caution 的元素
- **THEN** 避开这些元素

#### Scenario: 当前层级无可用项
- **WHEN** 当前层级无可用 menu_item
- **THEN** 决策执行 back 到父级

### Requirement: Prompt 模板
系统 SHALL 为上下文决策提供优化的 Prompt 模板。

#### Scenario: 系统提示词
- **WHEN** 获取系统 Prompt
- **THEN** 包含以下内容：
  - 可用动作列表和说明
  - 坐标定位规则
  - 决策原则（弹窗处理、异常恢复、分支选择）
  - 安全约束（绝对遵守）
  - 输出 JSON 格式规范

#### Scenario: 用户提示词
- **WHEN** 获取用户 Prompt
- **THEN** 包含以下占位符：
  - `{reason}`: 决策触发原因
  - `{current_path}`: 当前路径
  - `{is_popup}`: 是否弹窗
  - `{popup_info}`: 弹窗详情
  - `{elements_detail}`: 可用元素详情
  - `{overall_safe_to_proceed}`: 整体安全状态
  - `{safe_elements}`: 安全元素列表
  - `{caution_elements}`: 谨慎元素列表
  - `{skip_elements}`: 禁止元素列表
  - `{special_precautions}`: 特殊注意事项
  - `{node_stack}`: 节点栈
  - `{visited_pages}`: 已访问页面
  - `{failed_nodes}`: 失败节点
  - `{action_history}`: 最近操作历史
  - `{extra}`: 额外信息

### Requirement: 响应 Schema
系统 SHALL 定义上下文决策的 JSON Schema。

#### Scenario: Schema 验证
- **WHEN** AI 返回响应
- **THEN** 响应符合以下 Schema：
  - `result`: 决策结果枚举（必需）
  - `action`: 动作类型枚举（必需）
  - `target`: 对象或 null（可选）
  - `params`: 对象或 null（可选）
  - `reasoning`: 字符串（必需）
  - `confidence`: 0.0-1.0 数字（必需）
  - `safety_verified`: 布尔值（必需）

### Requirement: 解析器注册
系统 SHALL 注册 `ContextDecisionResult` 的解析器。

#### Scenario: 解析器函数
- **WHEN** 注册解析器
- **THEN** 解析器将 JSON 响应转换为 `ContextDecisionResult` 数据对象
- **AND** 验证 action 和 target 的组合有效性
