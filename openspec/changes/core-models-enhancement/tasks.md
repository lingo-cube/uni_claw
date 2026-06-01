## 0. 测试清理与资产准备

- [x] 0.1 审查现有测试文件，创建文件清单和分类
- [x] 0.2 识别待删除的过时测试文件
- [x] 0.3 识别待迁移的模型测试文件
- [x] 0.4 创建 `tests/assets/` 目录结构
- [x] 0.5 创建 `tests/assets/fixtures/` 目录
- [x] 0.6 创建 `tests/assets/utils/` 目录
- [x] 0.7 创建 `tests/models/` 目录
- [x] 0.8 创建 `tests/models/__init__.py`
- [x] 0.9 创建 `tests/archive/` 目录（用于归档旧测试）

## 1. 测试资产创建

- [x] 1.1 创建 `tests/assets/utils/model_helpers.py` - 模型测试辅助函数
- [x] 1.2 创建 `tests/assets/utils/assertions.py` - 自定义模型断言
- [x] 1.3 创建 `tests/assets/fixtures/page_analysis.json` - 页面分析样本数据
- [x] 1.4 创建 `tests/assets/fixtures/graph_nodes.json` - 图节点样本数据
- [x] 1.5 创建 `tests/assets/fixtures/state_machines.json` - 状态机样本数据
- [x] 1.6 创建 `tests/assets/fixtures/trace_data.json` - Trace 样本数据
- [x] 1.7 创建 `tests/assets/fixtures/ai_data.json` - AI 能力样本数据
- [x] 1.8 创建 `tests/assets/__init__.py`
- [x] 1.9 创建 `tests/assets/utils/__init__.py`

## 2. 测试文件清理

- [x] 2.1 审查 `tests/test_state.py` - 确认是否可删除或迁移
- [x] 2.2 审查 `tests/test_traversal_context.py` - 迁移到 `tests/models/test_context.py`
- [x] 2.3 审查 `tests/test_ai_types.py` - 迁移到 `tests/models/test_ai_types.py`
- [x] 2.4 删除确认无用的测试文件
- [x] 2.5 将过时但有价值的测试移动到 `tests/archive/`
- [x] 2.6 运行测试套件确保删除后仍正常工作

## 3. 枚举辅助方法实现

- [x] 3.1 为 `Direction` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.2 为 `MenuItemType` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.3 为 `ExpectedAction` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.4 为 `NodeType` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.5 为 `ChildrenStrategyType` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.6 为 `GlobalState` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.7 为 `TraversalState` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.8 为 `ExceptionSeverity` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.9 为 `ExceptionAction` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.10 为 `RecoveryAction` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.11 为 `DecisionResult` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法
- [x] 3.12 为 `ExecutionStatus` 枚举添加 `values()`, `from_value()`, `is_valid()` 方法

## 4. 枚举辅助方法测试

- [x] 4.1 在 `tests/assets/utils/assertions.py` 中添加枚举测试辅助函数
- [x] 4.2 创建 `tests/models/test_enums.py` 统一测试文件
- [x] 4.3 实现 `values()` 方法测试（所有枚举）
- [x] 4.4 实现 `from_value()` 方法测试（有效值和无效值场景）
- [x] 4.5 实现 `is_valid()` 方法测试（True 和 False 场景）
- [x] 4.6 实现空枚举边界测试
- [x] 4.7 运行测试并确保通过

## 5. Dataclass 模型字段验证

- [x] 5.1 为 `Target` dataclass 添加 `__post_init__` 验证
- [x] 5.2 为 `RestoreAction` dataclass 添加 `__post_init__` 验证
- [x] 5.3 为 `Precondition` dataclass 添加 `__post_init__` 验证
- [x] 5.4 为 `DynamicRule` dataclass 添加 `__post_init__` 验证
- [x] 5.5 为 `ChildrenStrategy` dataclass 添加 `__post_init__` 验证
- [x] 5.6 为 `ErrorPolicy` dataclass 添加 `__post_init__` 验证
- [x] 5.7 为 `TraversalNode` dataclass 添加 `__post_init__` 验证
- [x] 5.8 为 `StackFrame` dataclass 添加 `__post_init__` 验证
- [x] 5.9 为其他 dataclass 模型添加必要的字段验证

## 6. 测试文件骨架创建（新结构）

- [x] 6.1 创建 `tests/models/test_content_tree.py`
- [x] 6.2 创建 `tests/models/test_graph_nodes.py`
- [x] 6.3 创建 `tests/models/test_state_machine.py`
- [x] 6.4 创建 `tests/models/test_context.py`
- [x] 6.5 创建 `tests/models/test_exception.py`
- [x] 6.6 创建 `tests/models/test_ai_types.py`
- [x] 6.7 创建 `tests/models/test_trace.py`

## 7. 模型测试用例实现（页面分析模型）

- [x] 7.1 实现 `Coordinate` 测试（字段验证、边界值）
- [x] 7.2 实现 `MenuInfo` 测试
- [x] 7.3 实现 `MenuItem` 测试（包含 ExpectedAction 字段测试）
- [x] 7.4 实现 `MenuItemType` 枚举测试
- [x] 7.5 实现 `ExpectedAction` 枚举测试
- [x] 7.6 实现 `PopupInfo` 测试
- [x] 7.7 实现 `PageAnalysis` 测试（完整字段验证）

## 8. 模型测试用例实现（图节点模型）

- [x] 8.1 实现 `Target` 测试（字段验证、by 字段约束）
- [x] 8.2 实现 `RestoreAction` 测试
- [x] 8.3 实现 `Operation` 测试（action 字段约束）
- [x] 8.4 实现 `Precondition` 测试（timeout_seconds 字段）
- [x] 8.5 实现 `DynamicRule` 测试
- [x] 8.6 实现 `ChildrenStrategy` 测试（max_children 字段）
- [x] 8.7 实现 `ErrorPolicy` 测试
- [x] 8.8 实现 `NodeType` 枚举测试
- [x] 8.9 实现 `ChildrenStrategyType` 枚举测试
- [x] 8.10 实现 `TraversalNode` 测试（辅助方法测试）

## 9. 模型测试用例实现（内容树模型）

- [x] 9.1 实现 `ContentNode` 测试
- [x] 9.2 实现 `ContentTree` 测试（方法测试）
- [x] 9.3 实现 `VisitFingerprint` 测试
- [x] 9.4 实现 `TraversalState`（持久化）测试

## 10. 模型测试用例实现（状态机模型）

- [x] 10.1 实现 `GlobalState` 枚举测试
- [x] 10.2 实现 `GlobalStateTransition` 测试
- [x] 10.3 实现 `GlobalStateMachine` 基础测试
- [x] 10.4 实现 `TraversalState` 枚举测试
- [x] 10.5 实现 `TraversalStateTransition` 测试
- [x] 10.6 实现 `TraversalStateMachine` 基础测试
- [x] 10.7 实现 `StackFrame` 测试
- [x] 10.8 实现 `NodeStack` 测试（方法测试）

## 11. 模型测试用例实现（运行时上下文模型）

- [x] 11.1 实现 `TraversalContext`（AI 版本）测试
- [x] 11.2 实现 `ErrorRecord` 测试
- [x] 11.3 实现 `ActionRecord` 测试

## 12. 模型测试用例实现（异常处理模型）

- [x] 12.1 实现 `ExceptionSeverity` 枚举测试
- [x] 12.2 实现 `ExceptionAction` 枚举测试
- [x] 12.3 实现 `RecoveryAction` 枚举测试
- [x] 12.4 实现 `ExceptionContext` 测试
- [x] 12.5 实现 `ExceptionHandlingResult` 测试（工厂方法测试）

## 13. 模型测试用例实现（AI 能力模型）

- [x] 13.1 实现 `DecisionResult` 枚举测试
- [x] 13.2 实现 `ContainerInference` 测试（frozen 验证、confidence 范围）
- [x] 13.3 实现 `TraversalPlan`（AI 版本）测试
- [x] 13.4 实现 `NodeOperation` 测试
- [x] 13.5 实现 `NodeStrategy` 测试
- [x] 13.6 实现 `SafetyEvaluation` 测试
- [x] 13.7 实现 `PageLevelGuidance` 测试
- [x] 13.8 实现 `SafetyScreeningResult` 测试
- [x] 13.9 实现 `PageTypeVerification` 测试
- [x] 13.10 实现 `MismatchDetails` 测试
- [x] 13.11 实现 `Suggestion` 测试
- [x] 13.12 实现 `ContextDecisionResult` 测试

## 14. 模型测试用例实现（Trace 模型）

- [x] 14.1 实现 `ExecutionStatus` 枚举测试
- [x] 14.2 实现 `TraceDecision` 测试
- [x] 14.3 实现 `TraceExecution` 测试
- [x] 14.4 实现 `TraceStep` 测试（序列化/反序列化）
- [x] 14.5 实现 `StateSnapshot` 测试（序列化/反序列化）
- [x] 14.6 实现 `SessionInfo` 测试
- [x] 14.7 实现 `TraceSummary` 测试
- [x] 14.8 实现 `TraversalTrace` 测试（完整功能测试）

## 15. 测试覆盖率和验证

- [x] 15.1 运行所有模型测试并生成覆盖率报告
- [x] 15.2 验证核心模型覆盖率达到 80% 以上
- [x] 15.3 验证辅助模型覆盖率达到 60% 以上
- [x] 15.4 修复覆盖率不足的测试用例
- [x] 15.5 运行现有测试套件确保兼容性

## 16. 测试资产文档

- [x] 16.1 编写 `tests/assets/README.md` 说明测试资产的使用方法
- [x] 16.2 为每个 fixture 文件添加格式说明
- [x] 16.3 为测试工具类添加 docstring

## 17. 文档更新

- [x] 17.1 更新 `docs/core_business_models.md` 文档，添加枚举辅助方法说明
- [x] 17.2 在各模型文档中添加测试覆盖率说明
- [x] 17.3 更新测试指南文档（如有）
- [x] 17.4 创建 `tests/README.md` 说明新的测试目录结构

## 18. 代码审查和合并

- [x] 18.1 提交代码变更 (准备工作完成，待用户执行)
- [ ] 18.2 创建 Pull Request (待用户执行)
- [ ] 18.3 通过代码审查 (待用户执行)
- [ ] 18.4 合并到主分支 (待用户执行)
