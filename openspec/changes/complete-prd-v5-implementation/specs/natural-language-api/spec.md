## ADDED Requirements

### Requirement: Natural Language Command Parsing

系统 SHALL 提供 `CommandParser` 类，支持解析自然语言命令。

Parser SHALL 支持以下操作类型：
- 点击（点击/Click）
- 输入（输入/Input）
- 等待（等待/Wait）
- 验证（验证/Verify）
- 滑动（滑动/Swipe）
- 返回（返回/Back）

Parser SHALL 支持连接词分割多个操作：然后/接着/之后。

Parser SHALL 返回 `List[Operation]` 操作序列。

#### Scenario: 解析点击操作
- **WHEN** 输入命令 "点击移动数据"
- **THEN** 返回包含一个 Operation 的列表
- **AND** Operation.type 为 "click"
- **AND** Operation.target 为 "移动数据"

#### Scenario: 解析路径点击
- **WHEN** 输入命令 "点击车辆设置/DiLink/互联/移动数据"
- **THEN** Operation.target 解析为路径 ["车辆设置", "DiLink", "互联"]
- **AND** Operation.element 为 "移动数据"

#### Scenario: 解析组合操作
- **WHEN** 输入命令 "点击移动数据，然后输入 test"
- **THEN** 返回包含两个 Operation 的列表
- **AND** 第一个 Operation.type 为 "click"
- **AND** 第二个 Operation.type 为 "input"

---

### Requirement: Path Navigation Support

系统 SHALL 支持层级路径表达。

路径 SHALL 使用 "/" 分隔符。

系统 SHALL 支持绝对路径（从根开始）和相对路径（从当前位置）。

系统 SHALL 在执行前验证路径可达性。

#### Scenario: 绝对路径导航
- **WHEN** 路径为 "车辆设置/DiLink/互联"
- **THEN** 系统从根节点导航到目标
- **AND** 执行目标操作

#### Scenario: 相对路径导航
- **WHEN** 路径为 "子菜单/设置"
- **AND** 当前在 "主菜单"
- **THEN** 系统从当前位置导航到 "主菜单/子菜单/设置"

#### Scenario: 路径不存在时返回错误
- **WHEN** 路径包含不存在的节点
- **THEN** 返回 ExecutionResult
- **AND** result.success 为 False
- **AND** result.message 包含 "路径不存在"

---

### Requirement: Operation Execution

系统 SHALL 提供 `OperationExecutor` 类执行解析后的操作。

Executor SHALL 与 HierarchicalStateMachine 集成。

Executor SHALL 按顺序执行操作列表。

如果某个操作失败且 `stop_on_failure` 为 True，后续操作 SHALL 不执行。

#### Scenario: 成功执行点击操作
- **WHEN** 执行点击操作
- **AND** 目标元素存在
- **THEN** 返回 ExecutionResult(success=True)
- **AND** message 包含 "已点击"

#### Scenario: 目标不存在时失败
- **WHEN** 执行点击操作
- **AND** 目标元素不存在
- **THEN** 返回 ExecutionResult(success=False)
- **AND** message 包含 "找不到元素"

#### Scenario: 输入操作执行
- **WHEN** 执行输入操作
- **AND** 输入文本为 "test123"
- **THEN** 调用 ADBClient.input_text("test123")
- **AND** 返回成功结果

---

### Requirement: Natural Language Entry Point

TraversalEngine SHALL 提供 `execute(command: str) -> ExecutionResult` 方法。

该方法 SHALL 为自然语言命令的主入口。

如果自然语言功能未启用，方法 SHALL 抛出 RuntimeError。

方法 SHALL 返回综合执行结果。

#### Scenario: 自然语言入口点
- **WHEN** 调用 engine.execute("点击移动数据")
- **THEN** 内部解析并执行命令
- **AND** 返回 ExecutionResult

#### Scenario: 功能未启用时抛出异常
- **WHEN** enable_natural_language 为 False
- **AND** 调用 engine.execute()
- **THEN** 抛出 RuntimeError
- **AND** 异常消息包含 "未启用"

---

### Requirement: Variable Support

系统 SHALL 支持变量定义和使用。

用户 SHALL 能够通过 `set_variable(name, value)` 定义变量。

命令 SHALL 使用 `${变量名}` 语法引用变量。

系统 SHALL 在解析时替换变量引用。

#### Scenario: 定义和使用变量
- **WHEN** 设置变量 "用户名" = "testuser"
- **AND** 命令为 "输入 ${用户名}"
- **THEN** 实际输入文本为 "testuser"

#### Scenario: 未定义变量时报错
- **WHEN** 命令包含 ${未定义变量}
- **THEN** 抛出 VariableUndefinedException

---

### Requirement: Fuzzy Matching

系统 SHALL 支持元素名称的模糊匹配。

模糊匹配 SHALL 使用编辑距离或相似度算法。

相似度阈值 SHALL 可配置（默认 0.8）。

如果多个候选匹配，系统 SHALL 选择最相似的一个。

#### Scenario: 模糊匹配成功
- **WHEN** 目标为 "移动数"（输入错误）
- **AND** 实际元素为 "移动数据"
- **AND** 相似度 >= 0.8
- **THEN** 匹配到 "移动数据"

#### Scenario: 模糊匹配失败
- **WHEN** 目标为 "完全不相关"
- **AND** 所有元素相似度 < 0.6
- **THEN** 返回"未找到匹配元素"

---

### Requirement: Verification Operation

系统 SHALL 支持"验证"操作类型。

验证操作 SHALL 检查元素状态或存在性。

支持的验证类型：
- 元素存在验证
- 状态验证（开关状态、文本内容）
- 数值验证（滑块值等）

验证失败 SHALL 导致 ExecutionResult.success 为 False。

#### Scenario: 验证元素存在
- **WHEN** 命令为 "验证移动数据存在"
- **AND** 元素确实存在
- **THEN** 返回成功结果

#### Scenario: 验证开关状态
- **WHEN** 命令为 "验证移动数据已开启"
- **AND** 开关状态为开启
- **THEN** 返回成功结果

#### Scenario: 验证失败
- **WHEN** 验证条件不满足
- **THEN** 返回失败结果
- **AND** message 包含"验证失败"

---

### Requirement: Batch Execution

系统 SHALL 支持批量命令执行。

系统 SHALL 接受命令列表或命令文件。

系统 SHALL 按顺序执行所有命令。

系统 SHALL 返回批量执行摘要。

#### Scenario: 批量执行成功
- **WHEN** 提供 3 个命令
- **AND** 所有命令执行成功
- **THEN** 返回摘要包含 success_count=3

#### Scenario: 批量执行部分失败
- **WHEN** 提供 3 个命令
- **AND** 1 个命令失败
- **THEN** 返回摘要包含 success_count=2, failure_count=1

---

### Requirement: Command Recording

系统 SHALL 支持命令录制功能。

系统 SHALL 能够将操作序列记录为自然语言命令。

记录 SHALL 包含完整的操作路径和参数。

系统 SHALL 支持记录回放。

#### Scenario: 录制操作序列
- **WHEN** 启用录制模式
- **AND** 执行了一系列操作
- **THEN** 生成对应的自然语言命令字符串

#### Scenario: 回放录制的命令
- **WHEN** 执行录制的命令字符串
- **THEN** 执行相同的操作序列
