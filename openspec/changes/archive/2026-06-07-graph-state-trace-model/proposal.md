## Why

uni-claw V3.0 已实现基础遍历能力，但其"基于当前位置"的线性逻辑与菜单层级结构强耦合，难以扩展到复杂场景（多级菜单、弹窗、跳转）。PRD V4.0 定义了基于图模型的状态机驱动系统，通过统一的节点抽象、清晰的状态管理和全链路 Trace 记录，提升系统的可扩展性、可维护性和可观测性。

## What Changes

- **图模型**：引入 `TraversalNode` 统一节点抽象，支持静态图（预定义菜单结构）和动态图（运行时匹配控件模板）
- **模板注册表**：可配置的 JSON 文件定义各类控件的标准行为，新增控件类型无需修改代码
- **状态机引擎**：三层状态机（全局、遍历、节点栈）管理遍历生命周期和深度优先遍历
- **Trace 系统**：全链路记录遍历过程，支持严格/决策/模拟三种回放模式
- **兼容性**：通过 `use_graph_mode` 配置开关，默认保持 V3.0 线性遍历模式

**无破坏性变更** - 新功能通过开关控制，不影响现有遍历流程。

## Capabilities

### New Capabilities
- `traversal-graph`: 统一遍历图模型，支持静态图和动态图，提供节点抽象和模板注册表
- `state-machine`: 三层状态机（全局、遍历、节点栈）管理遍历生命周期
- `trace-system`: 全链路 Trace 记录与回放，支持严格/决策/模拟三种模式
- `template-registry`: 可配置的控件模板注册表，支持动态匹配和实例化

### Modified Capabilities
- 无现有规范需要修改

## Impact

- **新增代码模块**：
  - `src/graph/` - 图模型（TraversalNode、模板注册表、动态匹配）
  - `src/state_machine/` - 状态机（全局状态机、遍历状态机、节点栈）
  - `src/trace/` - Trace 系统（记录器、回放引擎）

- **修改代码文件**：
  - `src/traversal/traversal_engine.py` - 添加 `use_graph_mode` 开关，集成状态机引擎
  - `src/state/state_manager.py` - 扩展字段支持节点栈

- **新增配置**：
  - `use_graph_mode: bool` - 图模式开关（默认 false）
  - `template_registry_path: str` - 模板注册表路径
  - `trace_config: dict` - Trace 配置（存储路径、保留数量）

- **新增依赖**：暂无外部依赖（纯架构重构）
