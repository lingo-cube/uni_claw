## Why

PRD_V5 定义了 uni-claw 作为图驱动的智能遍历引擎的完整架构，包括图模型、状态机、Trace 系统和 AI 增强能力。当前实施已完成核心功能（约 60%），但 **AI 相关能力全部缺失**，导致系统无法：

1. **智能化决策**：只能依赖规则，无法理解复杂场景
2. **自然语言交互**：测试人员需要编写代码，无法用自然语言描述测试用例
3. **安全防护**：缺乏多层安全验证，存在破坏性操作风险
4. **智能异常恢复**：异常处理仅靠规则链，无法应对未知场景

同时，PRD_V5.1 新设计的 **自然语言测试 API** 和 **AI 驱动异常处理**（Phase 2）也尚未实施。

现在补齐这些功能，使 uni-claw 成为真正意义上的 AI 驱动遍历引擎。

---

## What Changes

本次变更实现 PRD_V5 提案中的所有 AI 相关功能，以及 PRD_V5.1 新增的设计：

### 新增功能

1. **AIProvider 核心能力**
   - 能力1：自然语言 → 遍历计划（`parse_task_to_plan`）
   - 能力2：页面类型验证（`verify_page_type`）
   - 能力3：元素安全预筛（`screen_elements`）
   - 能力4：上下文决策（`make_decision`）

2. **安全策略系统（SafetyPolicy）**
   - 四层安全防护：元素预筛层、AI 内部层、全局过滤层、设备驱动层
   - 危险文本黑名单、操作白名单、坐标越界检查
   - 与 AIProvider 强制绑定

3. **自然语言测试 API**
   - 命令解析器：支持"点击"、"输入"、"等待"、"验证"等操作
   - 路径导航：支持层级路径表达（如"点击车辆设置/DiLink/互联/移动数据"）
   - 执行引擎：与状态机集成

4. **AI 驱动异常处理（Phase 2）**
   - `AIDrivenExceptionHandler`：基于截图和上下文的异常分析
   - `AIDecision`：六种决策类型（RETRY/SKIP/BACKTRACK/NAVIGATE/RECOVER/WAIT_AND_RETRY）
   - `AIDecisionLearner`：决策历史记录与学习

### 架构调整

- 新增 `src/ai/` 模块：AIProvider 接口与实现
- 新增 `src/safety/` 模块：安全策略核心
- 扩展 `src/exception/` 模块：集成 AI 驱动处理
- 新增 `src/nl/` 模块：自然语言执行器

### 集成变更

- 状态机集成 AI 决策点
- 异常处理链集成 AI Handler
- TraversalEngine 扩展自然语言接口

---

## Capabilities

### New Capabilities

本次变更引入以下新能力，每个能力将创建独立的 spec：

- `ai-provider`: AIProvider 四项核心能力接口与实现
- `safety-policy`: 四层安全防护系统
- `natural-language-api`: 自然语言测试命令解析与执行
- `ai-exception-handling`: AI 驱动的异常处理与决策学习

### Modified Capabilities

无。本次变更不修改现有能力的行为定义，仅在现有能力基础上添加 AI 增强层。

---

## Impact

### 代码影响

- **新增模块**：`src/ai/`, `src/safety/`, `src/nl/`
- **扩展模块**：`src/exception/`, `src/traversal/`
- **修改配置**：`TraversalConfig` 新增 AI 相关开关

### API 影响

- **新增 API**：
  - `AIProvider` 接口
  - `SafetyPolicy` 接口
  - `NaturalLanguageExecutor` 接口
  - `AIDrivenExceptionHandler` 接口

- **TraversallEngine 扩展**：
  - `execute(command: str)` 方法（自然语言入口）

### 依赖影响

- **AI 服务依赖**：
  - Anthropic Claude API（已有）
  - 或 MiMo Vision API（已有）

- **配置依赖**：
  - AI 配置文件（模型选择、置信度阈值等）
  - 安全策略配置文件（黑名单/白名单）

### 向后兼容性

- ✅ 完全兼容：所有新功能均为可选，默认禁用
- ✅ 现有测试无需修改
- ✅ 可渐进式启用（单独启用每个功能）
