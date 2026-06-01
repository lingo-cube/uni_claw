# Initial Implementation Baseline

## Why

记录 uni-claw 项目 V1 版本的初始实现基线，为后续功能扩展和状态机实现提供参考点。当前核心遍历功能已基本完成，需要正式归档已实现的功能范围。

## What Changes

本文档为记录型变更，不涉及代码修改，仅总结和归档当前已完成的功能：

### 核心功能模块

- **视觉服务（Vision Service）**
  - 多 Provider 支持：Claude (Anthropic)、MiMo (小米)、Mock
  - OpenAI 协议和 Anthropic 协议双端点支持
  - AI 驱动的页面分析和入口定位

- **ADB 设备控制**
  - 真实设备（RealADBClient）和模拟设备（MockADBClient）
  - 截图、点击、返回等核心操作
  - 设备状态检测

- **状态管理（State Management）**
  - TraversalState：遍历状态持久化
  - ContentTree：层级结构树生成
  - 路径追踪和访问记录（visited set）

- **遍历引擎（Traversal Engine）**
  - 入口定位（navigate_to_app）
  - 结构初始化（initialize_structure）
  - 主遍历循环（run_step/run）
  - 弹窗、跳转、无反馈等场景处理

- **异常处理**
  - ClickResult 枚举定义（弹窗、跳转、无反馈、正常、错误）
  - 连续错误检测（超过阈值终止）
  - 子控件回退机制

- **事件系统**
  - TraversalEvent 实时事件通知
  - 遍历进度可观测性

- **配置管理**
  - TraversalConfig 可配置参数
  - 环境变量支持（API Key、设备 ID 等）
  - CLI 命令行接口

### 已有限制

- 状态机（StateMachine）：未实现，仅设计文档存在
- 分层状态机（HierarchicalStateMachine）：未实现，仅设计文档存在
- 图像指纹验证：未实现
- 入口名称一致性验证：未实现
- 按钮类型区分处理：未实现

## Capabilities

### New Capabilities

记录已实现的核心能力，每个能力对应一个 spec 文档：

- **vision-service**: AI 视觉分析服务，支持多 Provider
- **adb-control**: Android 设备 ADB 控制接口
- **state-management**: 遍历状态管理和持久化
- **traversal-engine**: 核心 UI 遍历逻辑
- **exception-handling**: 异常场景检测和处理
- **event-system**: 遍历事件通知系统

### Modified Capabilities

无（这是初始基线记录）

## Impact

### 代码影响

- 不涉及代码修改，仅文档记录

### 文档影响

- 创建 proposal.md（本文档）
- 创建 design.md（架构设计总结）
- 创建 specs/*.md（各能力规范文档）
- 创建 tasks.md（实现任务记录）

### 后续工作

- 为状态机实现提供功能边界参考
- 为按钮类型区分提供现有架构上下文
- 为指纹验证集成提供入口点参考
