# Uni-Claw 项目架构总览

> **文档版本**: V1.0
> **创建日期**: 2026-06-02
> **项目**: Uni-Claw - AI驱动的移动UI自动化遍历框架

---

## 一、项目概述

Uni-Claw 是一个模块化、可测试的移动应用UI自动化遍历框架，通过结合AI视觉分析和ADB控制实现智能化的应用界面探索。

### 核心能力

- **AI视觉分析**: 使用多种视觉服务（Claude、MiMo）理解屏幕内容
- **ADB设备控制**: 通过Android Debug Bridge进行精确的设备交互
- **智能状态管理**: 支持缓存和断点恢复
- **异常处理**: 完善的错误恢复机制
- **可观测性**: 分布式追踪、指标收集和日志记录

---

## 二、目录结构

```
uni-claw/
├── src/                          # 源代码
│   ├── adb/                      # ADB客户端接口
│   ├── ai/                       # AI策略顾问
│   │   ├── capabilities/         # AI能力模块
│   │   ├── core/                 # AI基础设施
│   │   └── vision/               # 视觉服务
│   ├── analysis/                 # 可观测性分析
│   ├── config/                   # 配置管理
│   ├── context/                  # 遍历上下文
│   ├── exception/                # 异常处理
│   ├── graph/                    # 图节点模型
│   ├── safety/                   # 安全过滤
│   ├── state/                    # 状态管理
│   ├── state_machine/            # 状态机
│   ├── trace/                    # 分布式追踪
│   ├── traversal/                # 核心遍历引擎
│   ├── utils/                    # 工具函数
│   └── vision/                   # 视觉服务接口
├── tests/                        # 测试套件
│   ├── models/                   # 模型测试
│   ├── assets/                   # 测试资源
│   └── conftest.py               # Pytest配置
├── scripts/                      # 脚本工具
├── dashboards/                   # 可视化仪表板
├── docs/                         # 文档
├── openspec/                     # 变更规范
└── .results/                     # 运行结果（gitignore）
```

---

## 三、核心架构分层

```
┌─────────────────────────────────────────────────────────────┐
│                        应用层                                │
│  scripts/run.py, dashboards, 验证脚本                       │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                        引擎层                                │
│  traversal/TraversalEngine - 核心遍历逻辑                  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      能力提供层                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │
│  │   AI/       │  │  vision/    │  │   adb/      │       │
│  │ UniBrain    │  │ VisionSvc   │  │ ADBClient   │       │
│  └─────────────┘  └─────────────┘  └─────────────┘       │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      基础设施层                              │
│  state/, exception/, trace/, analysis/                     │
│  状态管理 | 异常处理 | 追踪记录 | 可观测性                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 四、核心模块详解

### 4.1 数据模型层 (state/, graph/)

#### 核心模型

| 模块 | 文件 | 主要类/功能 |
|------|------|------------|
| 页面分析 | `state/content_tree.py` | `PageAnalysis`, `ContentNode`, `ContentTree` |
| 图节点 | `graph/node.py` | `TraversalNode`, `Operation`, `Precondition` |
| 状态机 | `state_machine/` | `GlobalStateMachine`, `TraversalStateMachine` |
| 上下文 | `context/traversal_context.py` | `TraversalContext`, `ActionRecord` |
| 异常 | `exception/` | `ExceptionContext`, `ExceptionHandlingResult` |

#### 数据流

```
截屏 → 视觉分析 → PageAnalysis → ContentTree构建
                    ↓
              TraversalNode创建
                    ↓
            状态机推进 + 上下文更新
                    ↓
              ADB操作执行
                    ↓
              追踪记录写入
```

### 4.2 AI能力层 (ai/)

#### 架构设计

```
AIStrategyAdvisor (接口)
         ↓
    UniBrain (提供者)
         ↓
    ┌────────┼────────┐
    ↓        ↓        ↓
  5大核心能力模块
```

#### 五大能力模块

| 能力 | 文件 | 功能 |
|------|------|------|
| ParseToPlan | `capabilities/parse_to_plan.py` | 自然语言指令解析 |
| VerifyPageType | `capabilities/verify_page_type.py` | 页面类型验证 |
| ScreenSafety | `capabilities/screen_safety.py` | 安全性筛查 |
| VisionAnalysis | `capabilities/vision_analysis.py` | 视觉分析 |
| ContextDecision | `capabilities/context_decision.py` | 上下文决策 |

#### 基础设施

| 模块 | 功能 |
|------|------|
| `core/config.py` | 配置管理 |
| `core/llm_client.py` | DeepSeek API客户端 |
| `core/validator.py` | 响应验证 |
| `core/prompts.py` | 提示词注册表 |
| `vision/` | 视觉服务（Claude/Mock） |
| `metrics.py` | 指标收集和失败归档 |

### 4.3 遍历引擎层 (traversal/)

#### TraversalEngine

核心遍历逻辑，负责：
- 导航到目标应用
- 初始化UI结构
- 执行遍历步骤
- 处理异常和恢复

#### 状态机集成

```
GlobalStateMachine (全局状态)
    ↓
TraversalStateMachine (遍历状态)
    ↓
NodeStack (层级栈)
```

### 4.4 可观测性层 (analysis/, trace/)

#### 分布式追踪 (trace/)

- `models.py` - 追踪数据模型
- `recorder.py` - 追踪记录器
- `replay.py` - 追踪回放

#### 分析服务 (analysis/)

- `server.py` - Web仪表板服务器
- `metrics.py` - 指标收集
- `trace_analyzer.py` - 追踪分析
- `tree.py` - 遍历树构建
- `structured_logging.py` - 结构化日志

---

## 五、关键数据流

### 5.1 遍历流程

```
1. 初始化
   └─> navigate_to_app() - 导航到目标应用

2. 结构初始化
   └─> initialize_structure() - 分析并缓存一级菜单

3. 遍历循环
   while 未完成且步数 < max_steps:
       ├─> 获取当前页面分析
       ├─> AI决策下一步操作
       ├─> 安全性检查
       ├─> 执行ADB操作
       ├─> 记录追踪
       └─> 处理异常（如需要）

4. 完成
   └─> 生成遍历报告
```

### 5.2 异常处理流程

```
异常发生
    ↓
异常分类 (exception/)
    ↓
异常链处理
    ├─> 尝试处理器1
    ├─> 尝试处理器2
    └─> ...
    ↓
恢复成功？
    ├─> 是 → 继续遍历
    └─> 否 → AI顾问决策
         └─> 安全过滤 → 执行恢复操作
```

---

## 六、外部接口

### 6.1 视觉服务接口

```python
class VisionService(Protocol):
    def analyze_screenshot(image_data: bytes) -> PageAnalysis
    def find_app_entry(image_data: bytes, target: str) -> dict | None
```

**实现**: ClaudeVisionService, MiMoVisionService, MockVisionService

### 6.2 ADB客户端接口

```python
class ADBClient(Protocol):
    def is_connected() -> bool
    def get_screen_size() -> ScreenSize
    def tap(x: float, y: float) -> None
    def input_text(text: str) -> None
    def press_back() -> None
    # ...
```

**实现**: RealADBClient, MockADBClient

### 6.3 AI策略顾问接口

```python
class AIStrategyAdvisor(Protocol):
    def infer_container_type(ui, context) -> ContainerInference
    def decide_next_action(goal, ui, context) -> Tuple[DecisionResult, dict]
    def handle_exception(exception, ui, context) -> Tuple[DecisionResult, dict]
```

**实现**: UniBrain, NoOpAIAdvisor, MockAIAdvisor

---

## 七、测试架构

### 7.1 测试组织

```
tests/
├── models/              # 模型测试
│   ├── test_content_tree.py
│   ├── test_graph_nodes.py
│   ├── test_state_machine.py
│   ├── test_context.py
│   ├── test_exception.py
│   ├── test_ai_types.py
│   ├── test_trace.py
│   └── test_enums.py
├── assets/              # 测试资源
│   ├── fixtures/        # 数据固件
│   └── utils/           # 测试工具
└── conftest.py          # Pytest配置
```

### 7.2 验证脚本

- `scripts/verify_refactor.py` - 重构验证脚本
- `tests/README.md` - 测试文档和验证清单

---

## 八、可观测性工具

### 8.1 Dashboard

| 工具 | 位置 | 端口 |
|------|------|------|
| 简单仪表板 | `dashboards/simple_dashboard.py` | 8002 |
| 分析服务器 | `dashboards/analysis_server.py` | 8000 |
| 高级仪表板 | `src/analysis/dashboard.html` | 8000 |

### 8.2 数据存储

| 目录 | 内容 |
|------|------|
| `.results/sessions/` | 遍历会话结果（JSON） |
| `.results/reports/` | 遍历报告（HTML/Markdown） |
| `.traces/` | 分布式追踪（JSONL） |
| `.logs/` | 结构化日志（JSONL） |

---

## 九、配置管理

### 9.1 环境变量

```bash
# AI服务
DEEPSEEK_API_KEY          # DeepSeek API密钥
ANTHROPIC_API_KEY         # Anthropic API密钥
MIMO_API_KEY             # MiMo API密钥

# ADB
ADB_DEVICE_ID            # 目标设备ID

# 配置
VISION_PROVIDER          # 视觉服务提供商
AI_PROVIDER_MAX_CONCURRENT # AI并发数
AI_PROVIDER_TIMEOUT      # AI超时时间
```

### 9.2 配置加载

```python
# 从环境变量加载
from src.ai.config_loader import load_ai_config, load_vision_config
from src.config import get_settings

ai_config = load_ai_config()
vision_config = load_vision_config()
settings = get_settings()
```

---

## 十、设计原则

1. **接口驱动**: 核心组件使用抽象接口，便于测试和扩展
2. **依赖注入**: 通过构造函数注入依赖，提高可测试性
3. **状态分离**: 状态管理与业务逻辑分离
4. **事件驱动**: 实时遍历事件提供可观测性
5. **异常隔离**: 完善的异常分类和处理机制
6. **可观测性优先**: 内置追踪、指标和日志

---

## 十一、技术栈

| 类别 | 技术 |
|------|------|
| 语言 | Python 3.10+ |
| 测试 | pytest |
| 类型检查 | mypy |
| 代码格式 | black, ruff |
| AI服务 | DeepSeek, Anthropic Claude |
| 设备控制 | ADB (Android Debug Bridge) |
| 可视化 | HTML/JavaScript (Dashboard) |

---

## 十二、已归档的变更

项目使用 OpenSpec 工作流管理变更，已归档的主要变更包括：

1. **AI策略顾问 (Phase 1-2)** - AI决策支持系统
2. **异常处理** - 完善的异常链处理机制
3. **UniBrain AI提供者** - 统一AI服务接口
4. **初始实现基线** - 核心框架实现
5. **核心模型增强** - 枚举辅助方法和测试
6. **图-状态-追踪模型** - 状态机和追踪系统

---

## 附录：相关文档

| 文档 | 路径 |
|------|------|
| 核心业务模型 | `docs/core_business_models.md` |
| 层级状态机 | `docs/hierarchical_state_machine.md` |
| AI部署指南 | `docs/ai_deployment_guide.md` |
| 异常处理集成 | `docs/exception_handling_integration.md` |
| 测试指南 | `docs/TEST_GUIDE.md` |
| UniBrain文档 | `src/ai/README.md` |
| 测试文档 | `tests/README.md` |
| Dashboard文档 | `dashboards/README.md` |
