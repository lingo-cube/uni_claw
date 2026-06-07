## Context

当前 `SimulationRunner` 导入路径 `src.graph.graph_traversal_engine` 不存在，始终走 `_execute_fallback_simulation()` 手写 DFS。Mock 服务（`MockVisionService`、`MockActionExecutor`）是实现真实接口（`VisionService` ABC、`OperationExecutor` ABC）的独立类。旧版 `InMemoryTracer` 与 V6.3 Trace 系统完全隔离。

**约束**：
- V6.3 trace-integration 已完成，`TraceRecorder`/`MemoryStorage`/`TraceAnalyzer` 可直接使用
- 真实 `VisionService` ABC 位于 `src/vision/vision_service.py`
- 真实 `OperationExecutor` ABC 位于 `src/simulation/operation_executor.py`
- 状态机 handler 当前为占位符（V6.5 修复），仿真结果仅包含引擎级 span

## Goals / Non-Goals

**Goals:**
- 打通 `SimulationRunner → GraphTraversalEngine → TraversalStateMachine → TraceRecorder` 调用链
- Mock 服务实现真实接口，`isinstance` 检查通过
- 仿真结果通过 `TraceAnalyzer` 提取，不再直接访问 mock 内部状态
- 删除手写 DFS fallback（~250 行）

**Non-Goals:**
- 不修复状态机 handler 占位符（留给 V6.5）
- 不产生 AI call / execution span（留给 V6.5）
- 不修改 `VisionService` ABC 或 `OperationExecutor` ABC
- 不删除 `InMemoryTracer` / `Visualizer` 文件

## Decisions

### 1. MockVisionService 路径上下文注入

**决策**：通过 `set_path_context(List[str])` 方法由引擎更新当前路径，Mock 按路径查 `virtual_pages` 字典。

**理由**：
- `VisionService.analyze_screenshot(image_data: bytes)` 签名不传路径——真实服务从截图分析页面
- 仿真无截图，需要通过带外机制告知当前页面
- `set_path_context` 是最小侵入方式，不修改 ABC

**替代方案**：
- 修改 ABC 签名增加 path 参数：破坏接口，影响所有真实实现
- 在 Mock 中解析 `image_data` 取元数据：过度设计

### 2. MockActionExecutor 统一 execute() 入口

**决策**：删除 `tap/swipe/click/press_back` 等零散方法，统一为 `execute(ExecutionContext) -> ExecutionResult`。

**理由**：
- `OperationExecutor` ABC 定义 `execute()` 作为唯一操作入口
- `ExecutionContext` 封装了 `operation` 字典（包含 action/target/params）
- V6.5 状态机将调用 `execute()`，而非零散方法

**替代方案**：
- 保留零散方法 + 新增 `execute()`：两套接口，混乱
- 只用零散方法：不实现 ABC，引擎无法注入

### 3. TraceRecorder 替代 InMemoryTracer

**决策**：`SimulationRunner` 创建 `MemoryStorage` + `TraceRecorder`，传入引擎。引擎内部自动生成 trace。仿真结果从 `MemoryStorage` 读取。

**理由**：
- 引擎已有完整的 span 生成逻辑（`_record_state_transition`、`_record_step_start` 等）
- 仿真和生产走同一 Trace 路径
- `TraceAnalyzer` 提供结构化提取，替代原来直接读 `tracer.steps`

### 4. 删除 try/except fallback

**决策**：移除 `try: import ... except ImportError: self.engine = None` 和 `if self.engine: ... else: _execute_fallback_simulation()` 分支。

**理由**：
- 修复导入后 `GraphTraversalEngine` 始终可用
- fallback 是维护负担，两套代码路径无法保证等价

## Risks / Trade-offs

### 风险：引擎 `run()` 循环不终止
- **缓解**：状态机 handler 是占位符，`_should_continue()` 在 node_stack 为空时退出；Mock 操作瞬时完成（delay=0）

### 风险：旧测试依赖 InMemoryTracer API 大量失败
- **缓解**：Phase 4 统一迁移测试断言到 TraceAnalyzer

### 风险：`PageAnalysis` 构造可能失败
- **缓解**：MockVisionService 从 virtual_pages 查表后构造 `PageAnalysis` pydantic 模型，字段对齐由单元测试保证

### 权衡：V6.4 仿真 trace 缺少 AI/execution span
- **原因**：状态机 handler 仍为占位符，不调用 vision/action
- **接受**：V6.5 修复 handler 后自然产生这些 span

## Migration Plan

### 部署步骤
1. 重写 `mock_vision.py`（接口对齐）
2. 重写 `mock_action.py`（接口对齐）
3. 重写 `runner.py`（修复导入 + 集成 Trace + 删除 fallback）
4. 更新测试断言到 TraceAnalyzer
5. 运行全量 V6 测试确认无回归

### 回滚策略
- 恢复 `runner.py` / `mock_vision.py` / `mock_action.py` 到 git HEAD
- `InMemoryTracer` 文件保留未删除，可恢复引用

## Open Questions

无。所有设计决策已确认。
