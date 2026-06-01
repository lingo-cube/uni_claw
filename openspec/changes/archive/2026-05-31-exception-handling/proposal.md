# Exception Handling System

## Why

当前 uni-claw 项目缺乏统一的异常处理机制。遍历过程中遇到异常时，无法智能恢复，容易导致整个遍历失败。通过引入统一的异常处理体系，可以提高遍历的鲁棒性和成功率。

## What Changes

### 核心变更

1. **统一异常分类体系**
   - 定义 TraversalException 基类和系列子类
   - 按严重级别分类：INFO/WARNING/ERROR/CRITICAL/FATAL
   - 覆盖定位、操作、设备、界面、AI 相关异常

2. **异常处理器接口**
   - ExceptionHandler 抽象接口
   - 内置处理器：重试、回退、设备恢复、UI处理
   - 异常处理链：按优先级处理异常

3. **与遍历引擎集成**
   - TraversalEngine 集成异常处理链
   - 操作执行包装：自动捕获和处理异常
   - 异常历史记录和查询

4. **状态机集成**（配合状态机实现）
   - 异常触发状态转换
   - 异常恢复策略
   - 位置恢复机制

### 数据结构变更

```python
# 新增异常类
TraversalException
├── LocationException (元素定位)
├── OperationException (操作执行)
├── DeviceException (设备相关)
├── UIException (界面变化)
└── AIException (AI分析)

# 新增处理结果
ExceptionHandlingResult
├── action: RETRY/SKIP/BACKTRACK/RECOVER/TERMINATE/IGNORE
├── new_state: 状态转换目标
└── message: 处理信息
```

## Capabilities

### New Capabilities

- **exception-classification**: 统一的异常分类和严重级别定义
- **exception-handlers**: 可扩展的异常处理器体系
- **exception-chain**: 按优先级处理异常的处理链
- **exception-recovery**: 异常恢复策略和动作

### Modified Capabilities

- **traversal-engine**: 集成异常处理链，操作自动包装
- **state-management**: 记录异常历史，支持异常恢复

## Impact

### 代码影响

- 新增 `src/exception/` 模块
  - `exceptions.py`: 异常类定义
  - `handlers.py`: 处理器接口和实现
  - `chain.py`: 处理链
- 修改 `src/traversal/traversal_engine.py`: 集成异常处理
- 修改 `src/state/content_tree.py`: 增加异常历史记录

### API 影响

- TraversalEngine 方法签名不变，内部增加异常处理
- 新增异常类可能被用户代码使用

### 行为影响

- 遍历过程中遇到异常不再直接失败，而是智能恢复
- 异常历史可查询，便于调试和分析

### 依赖影响

- 无新增外部依赖
