# Uni-Claw 项目文档索引 (Claude)

> **项目**: Uni-Claw - AI驱动的移动UI自动化遍历框架
> **版本**: V6.0
> **更新日期**: 2026-06-04

---

## 📚 文档导航

### 快速开始

| 文档 | 路径 | 说明 |
|------|------|------|
| 项目概述 | [README.md](README.md) | 项目介绍、安装和基本用法 |
| 快速上手 | [docs/SETUP.md](docs/SETUP.md) | 开发环境配置指南 |
| 开发工作流程 | [docs/DEVELOPMENT_WORKFLOW.md](docs/DEVELOPMENT_WORKFLOW.md) | 开发规范和工作流程 🆕 |

### 系统架构

| 文档 | 路径 | 说明 |
|------|------|------|
| **架构总览** | [docs/architecture/ARCHITECTURE.md](docs/architecture/ARCHITECTURE.md) | **完整系统架构说明** |
| **V6架构** | [docs/architecture/ARCHITECTURE_V6.md](docs/architecture/ARCHITECTURE_V6.md) | **V6 仿真器与状态机架构** 🆕 |
| **异常处理** | [docs/architecture/concepts/exception-handling.md](docs/architecture/concepts/exception-handling.md) | **异常处理架构** 🆕 |

### 架构模块设计

| 模块 | 设计文档 | 说明 |
|------|----------|------|
| **AI模块** | [docs/architecture/modules/ai-design.md](docs/architecture/modules/ai-design.md) | AI服务设计 |
| **ADB模块** | [docs/architecture/modules/adb-design.md](docs/architecture/modules/adb-design.md) | ADB客户端设计 |
| **遍历引擎** | [docs/architecture/modules/traversal-design.md](docs/architecture/modules/traversal-design.md) | 遍历引擎设计 |
| **图模型** | [docs/architecture/modules/graph-design.md](docs/architecture/modules/graph-design.md) | 图模型设计 |
| **仿真模块** | [docs/architecture/modules/simulation-design.md](docs/architecture/modules/simulation-design.md) | 仿真测试设计 |
| **状态管理** | [docs/architecture/modules/state-design.md](docs/architecture/modules/state-design.md) | 状态管理设计 |
| **异常处理** | [docs/architecture/modules/exception-design.md](docs/architecture/modules/exception-design.md) | 异常处理设计 |
| **可观测性** | [docs/architecture/modules/trace-design.md](docs/architecture/modules/trace-design.md) | 追踪系统设计 |
| **视觉服务** | [docs/architecture/modules/vision-design.md](docs/architecture/modules/vision-design.md) | 视觉服务设计 |
| **安全过滤** | [docs/architecture/modules/safety-design.md](docs/architecture/modules/safety-design.md) | 安全过滤设计 |
| **上下文管理** | [docs/architecture/modules/context-design.md](docs/architecture/modules/context-design.md) | 上下文管理设计 |
| **配置管理** | [docs/architecture/modules/config-design.md](docs/architecture/modules/config-design.md) | 配置管理设计 |
| **分析模块** | [docs/architecture/modules/analysis-design.md](docs/architecture/modules/analysis-design.md) | 分析模块设计 |
| **工具模块** | [docs/architecture/modules/utils-design.md](docs/architecture/modules/utils-design.md) | 工具模块设计 |
| **核心模型** | [docs/architecture/modules/models-design.md](docs/architecture/modules/models-design.md) | 核心模型设计 |
| **状态机** | [docs/architecture/modules/state-machine-design.md](docs/architecture/modules/state-machine-design.md) | 状态机设计 |
| **模块总览** | [docs/architecture/modules/README.md](docs/architecture/modules/README.md) | 模块文档索引 |

### 架构概念

| 文档 | 路径 | 说明 |
|------|------|------|
| **层级状态机** | [docs/architecture/concepts/hierarchical-state-machine.md](docs/architecture/concepts/hierarchical-state-machine.md) | 层级状态机概念 |
| **状态机设计** | [docs/architecture/concepts/state-machine-design.md](docs/architecture/concepts/state-machine-design.md) | 状态机概念设计 |
| **异常处理** | [docs/architecture/concepts/exception-handling.md](docs/architecture/concepts/exception-handling.md) | 异常处理架构 🆕 |
| **可观测性** | [docs/architecture/concepts/observability.md](docs/architecture/concepts/observability.md) | 可观测性系统设计 |
| **图模型** | [docs/architecture/concepts/graph-model.md](docs/architecture/concepts/graph-model.md) | 图模型概念 |

### 功能模块

| 文档 | 路径 | 说明 |
|------|------|------|
| Simulation 模块 | [docs/architecture/modules/simulation-design.md](docs/architecture/modules/simulation-design.md) | **仿真测试模块设计** 🆕 |
| 仿真测试架构 | [docs/architecture/testing/e2e-simulation-architecture.md](docs/architecture/testing/e2e-simulation-architecture.md) | **E2E仿真测试架构** 🆕 |
| 自然语言API | [docs/natural_language_test_api.md](docs/natural_language_test_api.md) | 自然语言测试接口 |
| AI部署指南 | [docs/ai_deployment_guide.md](docs/ai_deployment_guide.md) | AI服务部署配置 |

### PRD 文档

| 文档 | 路径 | 说明 |
|------|------|------|
| **统一 PRD** ⭐ | [docs/PRD_UNIFIED.md](docs/PRD_UNIFIED.md) | **综合版 PRD，整合 V4-V6 全系列** 🆕 |
| **历史 PRD 归档** | [docs/archive/prd/](docs/archive/prd/) | **历史版本 PRD 已归档** 🆕 |

#### 历史版本参考

| 版本 | 文档 | 说明 |
|------|------|------|
| V5.0 | [docs/archive/prd/PRD_V5_0-initial.md](docs/archive/prd/PRD_V5_0-initial.md) | 初始版本 |
| V5.1 | [docs/archive/prd/PRD_V5_1-ai-integration.md](docs/archive/prd/PRD_V5_1-ai-integration.md) | AI集成版本 |
| V5.2 | [docs/archive/prd/PRD_V5_2-flattened-screen.md](docs/archive/prd/PRD_V5_2-flattened-screen.md) | 两步视觉管道 |
| V5.3 | [docs/archive/prd/PRD_V5_3-unibrain-refactoring.md](docs/archive/prd/PRD_V5_3-unibrain-refactoring.md) | UniBrain重构 |
| V6.0 | [docs/archive/prd/PRD_V6_0-simulation-testing.md](docs/archive/prd/PRD_V6_0-simulation-testing.md) | 仿真测试系统 |

### 测试文档

| 文档 | 路径 | 说明 |
|------|------|------|
| 测试指南 | [docs/TEST_GUIDE.md](docs/TEST_GUIDE.md) | 测试规范和指南 |
| 测试工作流程 | [docs/TESTING_WORKFLOWS.md](docs/TESTING_WORKFLOWS.md) | 常用测试工作流程 🆕 |
| 仿真测试指南 | [docs/SIMULATION_TESTING_GUIDE.md](docs/SIMULATION_TESTING_GUIDE.md) | 仿真测试系统指南 |
| V6测试 | [tests/v6/README.md](tests/v6/README.md) | V6 测试套件说明 🆕 |
| 验证文档 | [docs/validation/](docs/validation/) | **V6实施验证报告** 🆕✅ |
| Dashboard文档 | [dashboards/README.md](dashboards/README.md) | 可视化仪表板说明 |

### AI 模块文档

| 文档 | 路径 | 说明 |
|------|------|------|
| UniBrain文档 | [src/ai/README.md](src/ai/README.md) | AI服务提供者文档 |

---

## 🎯 Claude 辅助开发指南

### 项目上下文

Uni-Claw 是一个**模块化、可测试的移动应用UI自动化遍历框架**，主要特点：

- **核心功能**: 使用AI视觉分析和ADB控制实现智能化的应用界面遍历
- **技术栈**: Python 3.10+, ADB, DeepSeek/Anthropic AI
- **架构风格**: 接口驱动、依赖注入、事件驱动
- **V6新增**: 仿真模拟器、状态机扩展、可视化Trace

### 关键设计原则

1. **接口驱动设计** - 核心组件使用抽象接口
2. **依赖注入** - 提高可测试性
3. **状态分离** - 状态管理独立于业务逻辑
4. **事件驱动** - 实时遍历事件
5. **可观测性优先** - 内置追踪、指标和日志
6. **仿真优先** (V6) - 无需设备的测试验证能力

### 核心模块速查

| 模块 | 职责 | 关键文件 |
|------|------|----------|
| **AI服务** | AI策略决策 | `src/ai/` |
| **遍历引擎** | 核心遍历逻辑 | `src/traversal/` |
| **图遍历引擎** (V6) | 基于图的遍历执行 | `src/traversal/graph_engine.py` |
| **仿真模拟器** (V6) | 离线测试与验证 | `src/simulation/` |
| **状态管理** | 状态持久化 | `src/state/`, `src/state_machine/` |
| **异常处理** | 异常链处理 | `src/exception/` |
| **可观测性** | 追踪/指标/日志 | `src/trace/`, `src/analysis/` |
| **图模型** (V6) | 声明式遍历计划 | `src/graph/` |

### V6 新增模块

| 模块 | 职责 | 关键文件 |
|------|------|----------|
| **TraversalPlan** | 声明式遍历计划 | `src/graph/plan.py` |
| **GraphTraversalEngine** | 图遍历执行引擎 | `src/traversal/graph_engine.py` |
| **SimulationRunner** | 仿真运行器 | `src/simulation/runner.py` |
| **InMemoryTracer** | 内存Trace与可视化 | `src/simulation/visualizer.py` |
| **MockVisionService** | Mock视觉服务 | `src/simulation/mock_vision.py` |
| **MockActionExecutor** | Mock动作执行 | `src/simulation/mock_action.py` |

### 开发工作流

```bash
# 1. 开发前阅读
# - docs/architecture/ARCHITECTURE.md (了解整体架构)
# - docs/architecture/ARCHITECTURE_V6.md (了解V6架构)
# - 对应模块的 README (了解具体实现)

# 2. 代码风格
# - 接口定义优先 (Protocol/ABC)
# - 依赖注入
# - 类型注解

# 3. 测试
# - 运行验证脚本: python scripts/verify_refactor.py
# - 查看测试文档: tests/README.md
# - V6仿真测试: pytest tests/v6/ -v

# 4. 变更管理
# - 使用 OpenSpec 工作流: /opsx:propose
```

### V6 验证状态 (2026-06-05) ✅

**实施验证**: ✅ **成功完成**

- **单元测试**: 84/84 passing (100%)
- **仿真基础设施**: ✅ 生产就绪
- **测试数据**: 5/5 fixtures 验证通过
- **实施质量**: ✅ 优秀，无阻塞问题

**验证报告**: [docs/validation/final_report.md](docs/validation/final_report.md)

---

## 📂 OpenSpec 变更记录

### 活跃变更

| 变更 | 状态 | 说明 |
|------|------|------|
| button-type-differentiation | 活跃 | 按钮类型区分 |
| complete-prd-v5-implementation | 活跃 | PRD V5 完整实现 |
| graph-state-trace-model | 活跃 | 图-状态-追踪模型 |

### 已归档变更

| 变更 | 日期 | 说明 |
|------|------|------|
| core-models-enhancement | 2026-06-02 | 核心模型增强 |
| initial-implementation-baseline | 2026-06-01 | 初始实现基线 |
| unibrain-ai-provider | 2026-05-31 | UniBrain AI提供者 |
| exception-handling | 2026-05-31 | 异常处理机制 |
| ai-strategy-advisor-phase1-2 | 2026-05-31 | AI策略顾问 (阶段1-2) |

---

## 🔧 常用命令

### 开发命令

```bash
# 运行验证
python scripts/verify_refactor.py

# 启动仪表板
python dashboards/simple_dashboard.py

# 运行测试
pytest tests/models/ -v

# 检查覆盖率
pytest tests/models/ --cov=src --cov-report=term-missing
```

### OpenSpec 命令

```bash
# 提出新变更
/opsx:propose

# 应用变更
/opsx:apply

# 归档变更
/opsx:archive

# 探索模式
/opsx:explore
```

---

## 📝 文档贡献

### 添加新文档

1. **架构文档** → 放入 `docs/` 并更新本索引
2. **规格文档** → 使用 OpenSpec 工作流
3. **测试文档** → 放入 `tests/` 或 `docs/TEST_GUIDE.md`
4. **模块文档** → 放入对应模块的 `README.md`

### 文档规范

- 使用 Markdown 格式
- 包含创建/更新日期
- 说明适用范围和受众
- 提供代码示例

### PRD 维护规则

> ⚠️ **重要**: PRD 文档必须遵循严格的版本管理规则

#### 命名规范

PRD 文档必须使用版本前缀 + 描述命名：

```bash
docs/PRD_V{major}_{minor}-{description}.md
```

**示例**:
- `docs/PRD_V5_0-initial.md` - V5.0 初始版本
- `docs/PRD_V5_1-ai-integration.md` - V5.1 AI集成版本
- `docs/PRD_V6_0-refactoring.md` - V6.0 重构版本

#### 版本管理流程

1. **新建 PRD** → 使用版本前缀创建新文档
2. **更新 PRD** → 创建新版本文档，保留旧版本作为参考
3. **废弃 PRD** → 移至 `docs/archive/prd/` 并更新索引

#### 版本号规则

- **主版本号 (major)**: 重大架构变更或功能重构
- **次版本号 (minor)**: 新功能添加或重要更新
- **修订版本**: 文档修正或补充（不改变版本号，直接更新文档日期）

#### 当前活跃 PRD

| 版本 | 文档 | 状态 |
|------|------|------|
| V5.1 | `docs/PRD_V5_1.md` | 当前活跃 ✅ |
| V5.0 | `docs/PRD_V5_0.md` | 稳定版本 |

#### 迁移指南

将现有 PRD 文档迁移到新命名规范：

```bash
# 重命名现有文档（添加描述后缀）
mv docs/PRD_V5.md docs/PRD_V5_0-initial.md
mv docs/PRD_V5.1.md docs/PRD_V5_1-ai-integration.md
```

---

## 🤝 Claude 协作提示

### 代码理解优先级

当 Claude 需要理解代码时，优先级如下：

1. **CLAUDE.md** (本文档) - 项目上下文
2. **docs/architecture/ARCHITECTURE.md** - 系统架构
3. **模块 README** - 具体模块说明
4. **代码注释和类型注解** - 实现细节

### 常见任务指引

| 任务 | 推荐文档 |
|------|----------|
| 理解整体架构 | docs/architecture/ARCHITECTURE.md |
| 添加新功能 | docs/PRD_V5_1.md + 对应模块 README |
| 修复 Bug | tests/README.md + 异常处理文档 |
| 优化性能 | docs/OBSERVABILITY.md |
| 添加 AI 能力 | src/ai/README.md |
| 状态机相关 | docs/architecture/concepts/hierarchical-state-machine.md |

---

**最后更新**: 2026-06-04
**维护者**: Uni-Claw 开发团队

---

## 📋 文档重组说明 (2026-06-04)

本项目文档已进行重组，采用新的组织结构：

**新结构**:
- `docs/architecture/` - 架构文档总目录
  - `docs/architecture/modules/` - 17个模块设计文档
  - `docs/architecture/concepts/` - 架构概念文档
  - `docs/architecture/testing/` - 测试架构文档
- `docs/archive/` - 归档目录
  - `docs/archive/prd/` - 历史PRD归档
  - `docs/archive/docs/` - 过时的设计文档

**文档清理** (2026-06-04) 🆕:
- **整合重复文档**: 将3个异常处理文档整合为1个综合文档
- **明确文档分工**: 状态机文档分为概念层和实现层
- **归档过时文档**: core_business_models.md等已归档
- **重新定位文档**: TESTING_ARCHITECTURE.md移到architecture/testing/

**改进点**:
- 所有架构文档集中管理，提高可发现性
- CLAUDE.md 成为完整的文档导航中心
- 历史文档归档但保留参考
- 清晰的层级结构和无重复内容
- 概念与实现分离，文档职责更清晰
