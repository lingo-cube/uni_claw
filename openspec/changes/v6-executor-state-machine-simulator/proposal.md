## Why

V5.x 系列专注于 AI 集成和视觉管道优化，但核心遍历控制逻辑仍然依赖传统状态机。当前系统存在以下问题：

1. **控制逻辑分散** - 状态机、执行引擎、异常处理耦合紧密，难以测试和维护
2. **测试困难** - 必须连接真实设备才能测试遍历逻辑，开发迭代慢
3. **可视化缺失** - 遍历过程不透明，难以调试和理解执行流程
4. **计划不完整** - 缺少完成策略、退出条件等高级遍历控制能力

V6 的目标是构建一个**声明式、可测试、可观测**的图遍历框架。

## What Changes

### 新增能力

1. **完整的图模型** - 新增 TraversalPlan 顶层容器，支持声明式遍历计划定义
   - ExitCondition（容器退出条件）
   - CompletionPolicy（全局完成策略）
   - EntryPolicy（应用入口策略）
   - IntentSlots（AI 意图槽位）

2. **扩展的状态机** - 新增三个关键状态，明确所有执行路径
   - FRAME_COMPLETE：容器帧完成处理（支持 BACK/AUTO_ESCAPE/SKIP/ABORT）
   - ERROR_HANDLING：三层兜底异常处理（节点策略 → 异常链 → AI）
   - POPUP_HANDLING：弹窗检测与自动处理

3. **图遍历执行引擎** - GraphTraversalEngine 实现
   - 初始化流程（入口策略、等待条件）
   - 主循环（状态机驱动、Trace 记录）
   - 深度限制、缓存管理

4. **仿真模拟器** - 无需真实设备的测试与验证工具
   - MockVisionService、MockActionExecutor
   - SimulationRunner（虚拟执行引擎）
   - 多格式可视化输出（ASCII 树、Mermaid 图、HTML 报告）

5. **Trace 系统** - 详细的执行追踪
   - 状态转换记录
   - JSONL/HTML 导出

### 模型扩展

- **TraversalNode** 新增 `exit_condition` 字段
- **ErrorPolicy** 新增 `BACKTRACK` 动作值

### 无破坏性变更

所有新增功能向后兼容，不影响现有代码。

## Capabilities

### New Capabilities

- **traversal-plan**: 声明式遍历计划模型，包含入口策略、完成策略、根节点定义
- **graph-models**: 扩展图模型（ExitCondition、CompletionPolicy、EntryPolicy、IntentSlots）
- **frame-complete-state**: 容器帧完成状态处理，支持多种退出策略
- **error-handling-state**: 三层兜底异常处理机制
- **popup-handling-state**: 弹窗检测与自动处理
- **graph-traversal-engine**: 图遍历执行引擎，集成状态机、缓存、Trace
- **simulation-runner**: 仿真模拟器，支持离线测试
- **mock-components**: Mock 视觉和动作执行组件
- **trace-visualization**: 多格式 Trace 可视化输出

### Modified Capabilities

- **traversal-context**: 扩展上下文以支持页面缓存、深度限制
- **traversal-fsm**: 扩展状态机，新增三个状态（FRAME_COMPLETE、ERROR_HANDLING、POPUP_HANDLING）

## Impact

### 新增文件

```
src/
├── graph/
│   └── plan.py              # TraversalPlan 及相关模型
├── traversal/
│   └── graph_engine.py      # GraphTraversalEngine
├── simulation/
│   ├── __init__.py
│   ├── runner.py            # SimulationRunner
│   ├── mock_vision.py       # MockVisionService
│   ├── mock_action.py       # MockActionExecutor
│   └── visualizer.py        # 可视化输出
└── tests/v6/
    ├── test_graph_models.py
    ├── test_state_machine.py
    ├── test_executor.py
    ├── test_simulation.py
    └── test_examples.py
```

### 修改文件

- `src/graph/node.py` - 添加新枚举和数据类，扩展 TraversalNode
- `src/state_machine/traversal_fsm.py` - 添加新状态和转移
- `src/trace/recorder.py` - 扩展 Trace 格式

### 依赖

无新增外部依赖。使用现有 Python 标准库和数据类。

### 测试影响

- 新增 6 个单元测试文件
- 新增 3 个端到端仿真测试
- 所有现有测试应继续通过

## 预期收益

| 指标 | 当前 | 目标 | 改善 |
|------|------|------|------|
| 代码可测试性 | 30% | 90% | +200% |
| 调试效率 | 1x | 3x | +200% |
| 遍历可观测性 | 基础 | 详细 Trace | 全覆盖 |
| 离线测试覆盖率 | 0% | 80%+ | 新能力 |
