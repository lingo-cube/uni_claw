## Why

Uni-Claw 核心业务模型文档 (docs/core_business_models.md) 已完成对已实现 59+ 数据模型的系统性梳理，但存在以下问题：
1. **缺少测试计划** - 各模型没有对应的测试规范，无法验证实现的正确性
2. **枚举类型缺少便捷方法** - 枚举类型没有提供获取所有值列表的方法，影响使用体验
3. **模型验证不完整** - 部分模型缺少字段验证逻辑，可能导致运行时错误
4. **测试文件混乱** - 现有测试文件结构不清晰，部分测试已过时
5. **缺少测试资产** - 测试数据和工具类分散，没有统一管理

## What Changes

- 为所有核心业务模型添加测试计划
- 为所有枚举类型添加 `values()` 和 `from_value()` 类方法
- 补充模型字段验证逻辑
- 添加模型序列化/反序列化测试
- **清理旧的无用测试文件**
- **创建测试资产目录结构（tests/assets/）**
- **重组模型测试到统一目录（tests/models/）**

## Capabilities

### New Capabilities
- `model-testing`: 核心业务模型的测试规范
  - 定义各模型的测试用例和验证标准
  - 包括字段验证、序列化测试、边界条件测试
  - 建立测试资产管理体系

- `enum-helpers`: 枚举类型的辅助方法
  - 为所有枚举添加 `values()` 类方法获取所有枚举值列表
  - 为所有枚举添加 `from_value()` 类方法从字符串值创建枚举实例
  - 添加 `is_valid()` 类方法验证字符串值是否为有效枚举值

- `test-assets`: 测试资产管理
  - 统一的测试固件（fixtures）管理
  - 可复用的测试工具类
  - 测试数据样本

### Modified Capabilities
无修改现有能力，仅补充测试和辅助方法

## Impact

**受影响的代码模块**：
- `src/state/content_tree.py` - 页面分析模型
- `src/graph/node.py` - 图节点模型
- `src/state_machine/global_fsm.py` - 全局状态机
- `src/state_machine/traversal_fsm.py` - 遍历状态机
- `src/state_machine/node_stack.py` - 节点栈
- `src/context/traversal_context.py` - 运行时上下文
- `src/exception/exceptions.py` - 异常定义
- `src/exception/context.py` - 异常上下文
- `src/ai/types.py` - AI 基础类型
- `src/ai/capabilities/types.py` - AI 能力类型
- `src/trace/models.py` - Trace 模型

**受影响的测试文件**：
- 删除过时的测试文件（具体文件待审查后确定）
- 迁移现有模型测试到新结构
- 新增测试资产目录

**新增依赖**：
- 无新增外部依赖

**API 变更**：
- 所有枚举类型新增类方法（非破坏性变更）

**目录结构变更**：
```
tests/
├── assets/          # 新增：测试资产
│   ├── fixtures/    # 测试固件
│   └── utils/       # 测试工具
├── models/          # 新增：模型测试
└── archive/         # 新增：归档旧测试
```
