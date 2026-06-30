# Uni-Claw C# 重构方案

> **方案**: 在 `feature/refactor` 分支进行完全 C# 重构，`main` 保留 Python 继续使用
> **版本**: 2.0 | **日期**: 2026-06-30
> **基于**: origin/feature/refactor 分支重构准则

---

## 目录

1. [总体策略](#1-总体策略)
2. [核心设计原则](#2-核心设计原则)
3. [整体架构分层](#3-整体架构分层)
4. [Phase 1：核心定义 2.0](#4-phase-1核心定义-20)
5. [Phase 2：核心实现](#5-phase-2核心实现)
6. [Phase 3：集成与桥接](#6-phase-3集成与桥接)
7. [Phase 4：入口与 UI](#7-phase-4入口与-ui)
8. [Python → C# 映射参考](#8-python--c-映射参考)
9. [命名约定](#9-命名约定)
10. [core-models.md 审计修复清单](#10-core-modelsmd-审计修复清单)

---

## 1. 总体策略

### 分支策略

| 分支 | 用途 |
|------|------|
| `main` | Python 系统持续运行、bug 修复 |
| `feature/refactor` | C# 完全重构，纯净 .NET 8 代码库 |

### 策略：Clean Slate 重写

完全脱离 Python，分四阶段构建 C# 系统：

- **Phase 1**：核心定义 — 接口 + 不可变模型，无实现，无外部依赖
- **Phase 2**：核心实现 — 状态机、图引擎、模板注册表
- **Phase 3**：集成 — Python AI 桥接、ADB、视觉服务
- **Phase 4**：入口 — Console 应用、DI 容器、配置系统

---

## 2. 核心设计原则

来自 `origin/feature/refactor` 分支 `src/UniClaw.Core/README.md`：

### 原则 1：接口优先

> 所有核心能力定义为接口，无具体实现

每个核心子系统先定义接口（放在 `Domain/`），具体实现放在对应项目。消费者只依赖接口，不依赖实现。

### 原则 2：类型安全

> 使用 C# 强类型系统和 record 类型

消灭所有 `string`/`dict` 参数。所有分类值转 `enum`，所有数据结构转 `record`。

### 原则 3：不可变性

> 使用 `readonly record struct` 和 `sealed record class` 确保不可变

- **值类型**（小数据、数学/几何）：`readonly record struct` — 如 `BoundingBox`
- **引用类型**（聚合、领域模型）：`sealed record class` — 如 `TraversalNode`
- 集合字段使用 `ImmutableArray<T>` 或 `IReadOnlyList<T>`，构造时拷贝，禁止 `List<T>` 暴露修改点

### 原则 4：依赖注入

> 通过构造函数注入依赖

实现类构造函数只依赖 `Domain` 中定义的接口，不依赖具体实现。使用 `Microsoft.Extensions.DependencyInjection` 装配。

---

## 3. 整体架构分层

```
UniClaw.Core/                          (.NET 8 Solution)
│
├── Domain/                            # 领域层 - 零外部依赖，纯接口 + record/struct
│   ├── Models/
│   │   ├── Vision/                    # TypeHint, BoundingBox, FlattenedElement, FlattenedScreen,
│   │   │                              #   Region, SelectionState, ScreenHints
│   │   ├── Common/                    # Operation, Target, RestoreAction, MenuItem, SimulationState
│   │   ├── Traversal/                 # TraversalNode, TraversalPlan, Template, Strategies
│   │   ├── AI/                        # ContainerInference, SafetyEvaluation, ContextDecisionResult,
│   │   │                              #   PageTypeVerification, PageLevelGuidance, MismatchDetails, Suggestion
│   │   ├── StateMachine/             # GlobalState, TraversalState, StateTransitionResult
│   │   ├── Tracing/                   # TraceNode, SessionNode, StepNode, SpanNode,
│   │   │                              #   PageTransitionSpan, AICallTrace
│   │   └── Exception/                 # ExceptionSeverity, TraversalException 层级
│   │
│   ├── StateMachine/                  # 状态机接口
│   │   ├── IGlobalStateMachine.cs
│   │   ├── ITraversalStateMachine.cs
│   │   ├── ITraversalContext.cs
│   │   └── INodeStack.cs
│   │
│   ├── Graph/                         # 图引擎接口
│   │   ├── IGraphTraversalEngine.cs
│   │   ├── IActionExecutor.cs
│   │   └── ITemplateRegistry.cs
│   │
│   ├── AI/                            # AI 策略接口
│   │   └── IAIStrategyAdvisor.cs
│   │
│   └── Observability/                 # 可观测性接口
│       ├── ITraceRecorder.cs
│       └── IMetricsCollector.cs
│
├── StateMachine/                      # 状态机实现 - 仅依赖 Domain
│   ├── GlobalStateMachine.cs          # IGlobalStateMachine 实现
│   ├── TraversalStateMachine.cs       # ITraversalStateMachine 实现
│   └── NodeStack.cs                   # INodeStack 实现
│
├── Graph/                             # 图引擎实现 - 依赖 Domain
│   ├── GraphTraversalEngine.cs        # IGraphTraversalEngine 实现
│   ├── ActionExecutor.cs              # IActionExecutor 实现（Phase 2 Stub → Phase 3 ADB）
│   └── TemplateRegistry.cs            # ITemplateRegistry 实现
│
├── AI/                                # AI 适配实现
│   └── AIStrategyAdvisor.cs           # IAIStrategyAdvisor 实现（Phase 3 进程桥接）
│
├── Infrastructure/                    # 基础设施 - 外部依赖
│   ├── Adb/                           # ADB 客户端封装（Phase 3）
│   ├── Vision/                        # 截图、OCR 服务（Phase 3）
│   ├── Serialization/                 # 序列化工具（独立于领域模型）
│   │   ├── DictionaryModelSerializer.cs
│   │   ├── SerializationExtensions.cs
│   │   └── PythonBridgeSerializer.cs  # Python 通信专用（唯一知 snake_case 处）
│   └── Telemetry/                     # Trace / Metrics 实现（Phase 3）
│
└── Host/                              # 入口（Phase 4）
    └── Console/                       # 控制台应用
```

### 依赖方向

```
Host → Infrastructure → AI/Graph/StateMachine → Domain
                                                ↑
                                          所有实现只依赖 Domain 接口
                                          Domain 零外部依赖
```

---

## 4. Phase 1：核心定义 2.0

### 目标

在 refactor 分支现有基础上，修正结构问题 + 补全遗漏模型。产出：所有接口在 `Domain/`，所有实现类文件独立，异常体系完整，序列化逻辑与模型分离。

### 4.1 工作项总览

```
Phase 1
├── P1.1 接口与实现分离
├── P1.2 模型文件拆分（按单一职责）
├── P1.3 补全遗漏模型
├── P1.4 序列化逻辑分离
└── P1.5 命名约定统一
```

### 4.2 P1.1 接口与实现分离

将接口从实现文件中移入 `Domain/`：

| 接口 | 当前位置 | 目标位置 |
|------|----------|----------|
| `IGlobalStateMachine` | `StateMachine/GlobalState.cs` | `Domain/StateMachine/IGlobalStateMachine.cs` |
| `ITraversalStateMachine` | `StateMachine/TraversalState.cs` | `Domain/StateMachine/ITraversalStateMachine.cs` |
| `ITraversalContext` | `StateMachine/TraversalState.cs` | `Domain/StateMachine/ITraversalContext.cs` |
| `INodeStack` | `StateMachine/TraversalState.cs` | `Domain/StateMachine/INodeStack.cs` |
| `IGraphTraversalEngine` | `Traversal/IGraphTraversalEngine.cs` | `Domain/Graph/IGraphTraversalEngine.cs` |
| `IActionExecutor` | `Traversal/IGraphTraversalEngine.cs` | `Domain/Graph/IActionExecutor.cs` |
| `ITemplateRegistry` | `Graph/Models/Template.cs` | `Domain/Graph/ITemplateRegistry.cs` |
| `IAIStrategyAdvisor` | `AI/IAIStrategyAdvisor.cs` | `Domain/AI/IAIStrategyAdvisor.cs` |
| `ITraceRecorder` | `Observability/ITraceRecorder.cs` | `Domain/Observability/ITraceRecorder.cs` |
| `IMetricsCollector` | `Observability/ITraceRecorder.cs` | `Domain/Observability/IMetricsCollector.cs` |

具体实现保留在原位置：
- `NodeStack` → `StateMachine/NodeStack.cs`
- `PlaceholderResolver` / `TemplateValidator` → `Graph/`
- 所有序列化逻辑 → `Infrastructure/Serialization/`

### 4.3 P1.2 模型文件拆分

按单一职责原则，将大文件拆散为独立文件。

#### Domain/Models/Vision/

| 文件 | 内容 |
|------|------|
| `TypeHint.cs` | `TypeHint` 枚举 + `TypeHintExtensions` 扩展方法 |
| `BoundingBox.cs` | `BoundingBox` (readonly record struct) + `BoundingBoxPixel` |
| `FlattenedElement.cs` | `FlattenedElement` 单 UI 元素模型 |
| `FlattenedScreen.cs` | `FlattenedScreen` 整屏模型 + 查询方法 |
| `Region.cs` | `Region` + `RegionRole` 枚举 |
| `SelectionState.cs` | `SelectionState` 枚举 + 扩展方法 |
| `ScreenHints.cs` | `ScreenHints` 屏幕级元数据 |

#### Domain/Models/Common/

| 文件 | 内容 |
|------|------|
| `Operation.cs` | `OperationType` 枚举 + `Operation` record |
| `Target.cs` | `TargetType` 枚举 + `Target` record |
| `RestoreAction.cs` | `RestoreAction` record |
| `MenuItem.cs` | `MenuItem` record（含 `type`、`expected_action`、`expects_page_change`、`expects_state_change` 及默认值） |
| `Coordinate.cs` | `Coordinate` record（归一化坐标 0-1） |
| `SimulationState.cs` | `SimulationState` record（字段默认值用 `[]` 初始化） |

#### Domain/Models/Traversal/

| 文件 | 内容 |
|------|------|
| `TraversalNode.cs` | `TraversalNode` record + `NodeType` 枚举 |
| `TraversalPlan.cs` | `TraversalPlan` record + `EntryPolicy` + `CompletionPolicy` + `IntentSlots` |
| `Template.cs` | `Template` record |
| `Strategies.cs` | `ChildrenStrategy`, `ErrorPolicy`, `ExitCondition`, `Precondition`, `DynamicRule`, `MatchCondition` |
| `TargetFoundAction.cs` | `TargetFoundAction` 枚举 |
| `EntryConfig.cs` | `EntryConfig` record |
| `MatchMode.cs` | `MatchMode` 枚举 |

#### Domain/Models/StateMachine/

| 文件 | 内容 |
|------|------|
| `GlobalState.cs` | `GlobalState` 枚举 + `StateTransitionResult` record + `StateTransitionEventArgs` |
| `TraversalState.cs` | `TraversalState` 枚举 + `PageRelation` 枚举 |

#### Domain/Models/AI/

| 文件 | 内容 |
|------|------|
| `ContainerInference.cs` | `ContainerInference` record |
| `SafetyEvaluation.cs` | `SafetyEvaluation` record |
| `SafetyScreeningResult.cs` | `SafetyScreeningResult` record |
| `PageTypeVerification.cs` | `PageTypeVerification` record |
| `PageLevelGuidance.cs` | `PageLevelGuidance` record |
| `ContextDecisionResult.cs` | `ContextDecisionResult` record |
| `MismatchDetails.cs` | `MismatchDetails` record |
| `Suggestion.cs` | `Suggestion` record |
| `DecisionResult.cs` | `DecisionResult` 枚举 |

#### Domain/Models/Tracing/

| 文件 | 内容 |
|------|------|
| `TraceNode.cs` | `TraceNode` 基类 record |
| `SessionNode.cs` | `SessionNode` record |
| `StepNode.cs` | `StepNode` record |
| `SpanNode.cs` | `SpanNode` record |
| `PageTransitionSpan.cs` | `PageTransitionSpan` record |
| `AICallTrace.cs` | `AICallTrace` record |

#### Domain/Models/Exception/

| 文件 | 内容 |
|------|------|
| `ExceptionSeverity.cs` | `ExceptionSeverity` 枚举（Info / Warning / Error / Critical / Fatal） |
| `TraversalException.cs` | `TraversalException` 异常基类层级 |

### 4.4 P1.3 补全遗漏模型

对照 Python 代码库，Phase 1 当前缺失的模型全部补全到对应命名空间（见 4.3 中标注"新建"的文件）。

关键补充：
- **AI 模型**（7 个）：`ContainerInference`、`SafetyEvaluation`、`SafetyScreeningResult`、`PageTypeVerification`、`PageLevelGuidance`、`ContextDecisionResult`、`MismatchDetails`、`Suggestion`、`DecisionResult`
- **Tracing 模型**（6 个）：`TraceNode` 层级（含 `SessionNode`、`StepNode`、`SpanNode`、`PageTransitionSpan`）、`AICallTrace`
- **Exception 模型**（2 个）：`ExceptionSeverity` 枚举、`TraversalException` 异常层级
- **Common 模型补充**：`MenuItem`（含默认值）、`SimulationState`（含默认值）
- **Traversal 模型补充**：`TargetFoundAction` 枚举

### 4.5 P1.4 序列化逻辑分离

将 `ToDictionary()`/`FromDictionary()` 从领域模型中移除，抽取到独立序列化层：

```
Infrastructure/Serialization/
├── DictionaryModelSerializer.cs   # 静态扩展类，模型 ↔ Dictionary<string, object?>
├── SerializationExtensions.cs     # 通用 JSON 序列化工具（C# 默认行为，PascalCase）
└── PythonBridgeSerializer.cs      # Python 通信专用序列化（唯一知道 snake_case 的地方，Phase 3 启用）
```

核心规则：

- **C# 内部一切使用 PascalCase**，不做任何枚举值映射
- **枚举 `ToString()` = PascalCase**，JSON 序列化默认输出 PascalCase
- **Python 兼容性只在 `PythonBridgeSerializer` 一个文件中处理**，不注册全局策略
- 领域模型保持纯净，不耦合序列化逻辑，也不知 Python 命名规则

### 4.6 P1.5 命名约定

| 类别 | 约定 | 示例 |
|------|------|------|
| 枚举值 | **PascalCase，不做映射** | `AllChildrenVisited`（C# 全域统一，无需 snake_case 转换） |
| 枚举扩展 | `{EnumName}Extensions` | `TypeHintExtensions` |
| 接口 | `I` 前缀 | `IGlobalStateMachine` |
| 领域模型 | 不加前缀/后缀 | `TraversalNode`（非 `TraversalNodeModel`） |
| 文件 | 一文件一主类 | `TypeHint.cs = TypeHint 枚举 + TypeHintExtensions` |
| 集合字段 | `IReadOnlyList<T>` | `FlattenedScreen.Elements` |
| 可选字段 | `T?` | `string?`（可空引用类型） |

> **枚举映射策略**：C# 内部始终使用 PascalCase，不注册全局 `SnakeCaseLower` 策略。
> Python 兼容性仅在 `Infrastructure/Serialization/PythonBridgeSerializer.cs` 中处理——该文件是唯一知道 Python 命名规则的地方。

### 4.7 Phase 1 产物清单

| 命名空间 | 文件数 | 说明 |
|----------|:---:|------|
| `Domain/Models/Vision/` | 7 | TypeHint、BoundingBox、FlattenedElement、FlattenedScreen、Region、SelectionState、ScreenHints |
| `Domain/Models/Common/` | 6 | Operation、Target、RestoreAction、MenuItem、Coordinate、SimulationState |
| `Domain/Models/Traversal/` | 7 | TraversalNode、TraversalPlan、Template、Strategies、TargetFoundAction、EntryConfig、MatchMode |
| `Domain/Models/StateMachine/` | 2 | GlobalState、TraversalState |
| `Domain/Models/AI/` | 10 | ContainerInference、SafetyEvaluation、SafetyScreeningResult、PageTypeVerification、PageLevelGuidance、ContextDecisionResult、MismatchDetails、Suggestion、DecisionResult、IAIStrategyAdvisor |
| `Domain/Models/Tracing/` | 6 | TraceNode、SessionNode、StepNode、SpanNode、PageTransitionSpan、AICallTrace |
| `Domain/Models/Exception/` | 2 | ExceptionSeverity、TraversalException |
| `Domain/StateMachine/` | 4 | IGlobalStateMachine、ITraversalStateMachine、ITraversalContext、INodeStack |
| `Domain/Graph/` | 3 | IGraphTraversalEngine、IActionExecutor、ITemplateRegistry |
| `Domain/Observability/` | 2 | ITraceRecorder、IMetricsCollector |
| **合计** | **~49** | |

---

## 5. Phase 2：核心实现

### 目标

在 Phase 1 的 Domain 接口和模型之上，实现所有核心状态机、图引擎、模板注册表。每个实现类**可独立编译、独立单元测试**，不依赖外部基础设施。

### 5.1 P2.1 GlobalStateMachine

```
文件：StateMachine/GlobalStateMachine.cs
实现：IGlobalStateMachine
依赖：仅 Domain 接口
职责：
  - 管理 GlobalState 枚举状态转换（Idle → Initializing → Traversing → ...）
  - 验证转换合法性（如不允许 Completed → Terminated）
  - 发布 StateTransitionEventArgs 事件
  - 提供 CurrentState / TransitionHistory 查询
测试：验证所有合法转换、拒绝非法转换、事件发布正确
参考：Python src/state_machine/global_fsm.py
```

### 5.2 P2.2 TraversalStateMachine

```
文件：StateMachine/TraversalStateMachine.cs
实现：ITraversalStateMachine
依赖：Domain 接口 + IGlobalStateMachine（接口注入）
职责：
  - 管理遍历状态机（NodeSelect → PreconditionCheck → Execute → ...）
  - 根据 TraversalStateTransition 决定下一状态
  - 调用 ITraversalContext 读写上下文
  - 发出 PageRelation 判定（Match / Navigable / Deeper / Unknown）
测试：每个状态的转换规则、边界条件、错误恢复路径
参考：Python src/state_machine/traversal_fsm.py
```

### 5.3 P2.3 TemplateRegistry

```
文件：Graph/TemplateRegistry.cs
实现：ITemplateRegistry（在 Domain/Graph/ 中定义）
依赖：仅 Domain 模型
职责：
  - 注册 / 查询 / 删除 Template 定义
  - 解析占位符 {{item_text}}、{{item_index}}、{{parent_id}}
  - 验证模板完整性（PlaceholderResolver + TemplateValidator）
  - 支持 DynamicRule 匹配
测试：模板注册查询、占位符解析、无效模板拒绝
```

### 5.4 P2.4 GraphTraversalEngine

```
文件：Graph/GraphTraversalEngine.cs
实现：IGraphTraversalEngine
依赖：ITraversalStateMachine + ITemplateRegistry + INodeStack + IActionExecutor
职责：
  - 接受 TraversalPlan 输入
  - 使用 INodeStack 管理遍历栈（push / pop / peek）
  - 对每个节点：检查 Precondition → 调用 IActionExecutor → 检查 ExitCondition
  - 处理 ErrorPolicy（retry / skip / abort / fallback / backtrack）
  - 返回 TraversalResult（成功 / 失败 / 终止原因）
测试：线性遍历、嵌套容器、错误恢复、退出条件触发
```

### 5.5 P2.5 ActionExecutor (Stub)

```
文件：Graph/ActionExecutor.cs
实现：IActionExecutor
依赖：仅 Domain 模型（Phase 2 无真实 ADB）
职责：
  - 解析 Operation（Click / Swipe / Back / InputText / Wait / LongPress）
  - Stub 模式：记录操作日志，返回模拟结果
  - 为 Phase 3 的 ADB 集成预留接口
测试：每种操作类型的日志输出、参数验证
```

### 5.6 P2.6 单元测试

每个实现类对应一个 xUnit 测试类。使用 Moq 或手写 stub 模拟所有接口依赖。覆盖率目标：> 85% 行覆盖。CI：`dotnet test` 在每次提交时运行。

### 5.7 Phase 2 产物清单

| 文件 | 类型 | 行数估算 |
|------|------|:---:|
| `StateMachine/GlobalStateMachine.cs` | 实现 | ~150 |
| `StateMachine/TraversalStateMachine.cs` | 实现 | ~250 |
| `Graph/TemplateRegistry.cs` | 实现 | ~200 |
| `Graph/GraphTraversalEngine.cs` | 实现 | ~350 |
| `Graph/ActionExecutor.cs` | Stub 实现 | ~100 |
| `tests/.../GlobalStateMachineTests.cs` | 测试 | ~200 |
| `tests/.../TraversalStateMachineTests.cs` | 测试 | ~300 |
| `tests/.../TemplateRegistryTests.cs` | 测试 | ~200 |
| `tests/.../GraphTraversalEngineTests.cs` | 测试 | ~350 |
| `tests/.../ActionExecutorTests.cs` | 测试 | ~150 |

---

## 6. Phase 3：集成与桥接

### 目标

将 C# 核心与 Python AI 模块、ADB 设备控制连接，形成可运行系统。

### 6.1 P3.1 Python AI 桥接

```
文件：AI/AIStrategyAdvisor.cs
实现：IAIStrategyAdvisor
依赖：Infrastructure/Serialization/PythonBridgeSerializer（枚举命名转换）
序列化：System.Diagnostics.Process（子进程 stdin/stdout JSON 或 gRPC）
职责：
  - 通过 PythonBridgeSerializer 将 C# 请求转为 Python 兼容 JSON（PascalCase → snake_case）
  - 通过子进程或 gRPC 调用 Python AI 模块
  - 通过 PythonBridgeSerializer 将 Python 响应转回 C# 类型（snake_case → PascalCase）
  - 处理通信超时、重试、降级

关键约束：
  - PythonBridgeSerializer 是唯一知道 Python 命名规则的地方
  - C# 内部所有枚举序列化默认使用 PascalCase，不注册全局 SnakeCaseLower
  - 如果 Python 端将来改为接受 PascalCase，只需删除 PythonBridgeSerializer 一个文件
```

### 6.2 P3.2 ADB 客户端

```
文件：Infrastructure/Adb/AdbClient.cs
依赖：System.Diagnostics.Process
职责：
  - 封装 ADB 命令：截图、点击、滑动、按键、输入文本
  - 提供结构化返回类型（非字符串解析）
  - 支持设备管理（连接、断开、多设备）
```

### 6.3 P3.3 视觉服务

```
文件：Infrastructure/Vision/ScreenCaptureService.cs
职责：
  - 通过 ADB 获取设备截图
  - 图像预处理（缩放、格式转换）
  - 为 AI 分析准备输入
```

### 6.4 P3.4 集成测试

```
端到端测试：C# 引擎 → Python AI → ADB → 设备
使用真实或模拟设备验证完整流程
```

---

## 7. Phase 4：入口与 UI

### 目标

提供可执行入口，通过 DI 容器装配所有组件，支持参数化配置。

### 7.1 P4.1 Console 应用

```
Host/Console/
├── Program.cs               # 入口点
├── AppHost.cs               # DI 容器装配
└── appsettings.json         # 配置文件
```

### 7.2 P4.2 配置系统

- 使用 `Microsoft.Extensions.Configuration`
- 支持 JSON 文件、环境变量、命令行参数
- 配置项：设备 ID、AI 提供商、遍历参数、日志级别

### 7.3 P4.3 DI 容器

- 使用 `Microsoft.Extensions.DependencyInjection`
- 所有实现按生命周期注册（Singleton: 状态机、引擎；Transient: 请求上下文）
- 支持 Mock 替换以进行测试

---

## 8. Python → C# 映射参考

### 类型映射

| Python | C# |
|--------|-----|
| `@dataclass` | `sealed record class` |
| `@dataclass(frozen=True)` | `readonly record struct` |
| `str, Enum` | `enum`（PascalCase，C# 内部不做映射；桥接层独立转换） |
| `Protocol` | `interface` |
| `List[T]` | `List<T>`（内部）/ `IReadOnlyList<T>`（公开字段） |
| `Dict[K, V]` | `Dictionary<K, V>` |
| `Optional[T]` | `T?`（可空引用类型） |
| `Tuple[T, U]` | `(T, U)`（值元组） |
| `set[T]` | `ImmutableHashSet<T>` |
| `Field(default_factory=...)` | 主构造函数默认值 或 `= []` |
| `BaseModel` | `sealed record class` |
| `__all__` | 命名空间可见性（`public` / `internal`） |
| `@staticmethod` | `static` 方法 |
| `@classmethod` | 静态工厂方法（如 `FromDictionary`） |

### 模块映射

| Python | C# |
|--------|-----|
| `src/models/vision/` | `Domain/Models/Vision/` |
| `src/models/content_models.py` | `Domain/Models/Common/` + `Domain/Models/Traversal/` |
| `src/ai/ai_types.py` | `Domain/Models/AI/` + `Domain/AI/` |
| `src/exception/exceptions.py` | `Domain/Models/Exception/` |
| `src/state_machine/global_fsm.py` | `Domain/StateMachine/` + `StateMachine/` |
| `src/state_machine/traversal_fsm.py` | `Domain/StateMachine/` + `StateMachine/` |
| `src/graph/node.py` | `Domain/Models/Traversal/` + `Domain/Graph/` |
| `src/trace/models.py` | `Domain/Models/Tracing/` |
| `src/trace/context.py` | `Domain/StateMachine/ITraversalContext.cs` |
| `src/traversal/` | `Graph/` |

---

## 9. 命名约定

### 文件组织

- **一文件一主类**：每个 `.cs` 文件包含一个主要类型及其扩展方法
- **扩展方法**：放在 `{ClassName}Extensions.cs` 中
- **接口文件**：`I{接口名}.cs`

### 命名风格

| 元素 | 风格 | 示例 |
|------|------|------|
| 命名空间 | PascalCase，点分隔 | `UniClaw.Core.Domain.Models.Vision` |
| 接口 | `I` 前缀 | `IGlobalStateMachine` |
| 类 / record | PascalCase | `TraversalNode` |
| 枚举类型 | PascalCase | `NodeType` |
| 枚举值 | PascalCase | `LeafSwitch`（非 `LEAF_SWITCH` 或 `leaf_switch`） |
| 方法 | PascalCase | `IsInteractive()` |
| 属性 | PascalCase | `CurrentState` |
| 参数 | camelCase | `cancellationToken` |
| 私有字段 | `_camelCase` | `_currentState` |

### 集合字段

公开的集合字段使用不可变类型：

```csharp
// ✅ 正确
public sealed record class FlattenedScreen(
    IReadOnlyList<FlattenedElement> Elements
);

// ❌ 错误
public sealed record class FlattenedScreen(
    List<FlattenedElement> Elements  // 暴露修改点
);
```

---

## 10. core-models.md 审计修复清单

以下是对照 `docs/architecture/core-models.md` 发现的差异，Phase 1 实施时逐一修复：

### 中等问题

| # | 文件 | 问题 | 修复方式 |
|---|------|------|----------|
| 1 | `Common/MenuItem.cs` | 缺失默认值：`type=MenuItemType.Item`、`expected_action=ExpectedAction.Action`、`expects_page_change=False`、`expects_state_change=False` | P1.3 新增文件时设定默认值 |
| 2 | `Common/SimulationState.cs` | 11 个字段缺失默认值/别名 | P1.3 新增文件时对齐 Python 实际代码 |
| 3 | `AI/` 命名空间 | 5 个类型未记录（MismatchDetails、Suggestion、PageTypeVerification、PageLevelGuidance、ContextDecisionResult） | P1.3 全部补齐 |
| 4 | `StateMachine/GlobalState.cs` | `GlobalStateTransition` 缺失 `reason` 字段和时间戳默认值 | P1.2 拆分时修正 |

### 轻微问题

| # | 问题 | 修复方式 |
|---|------|----------|
| 5 | `TraversalStateTransition.timestamp` 缺失默认值 | P1.2 修正 |
| 6 | `ContentTree.level_counters` 缺失 alias | C# 使用属性名，alias 不需要 |
| 7 | `TargetFoundAction` 枚举被引用但未定义 | P1.2 新增 `Traversal/TargetFoundAction.cs` |
| 8 | trace 模型 `SpanNode` 及子类未记录 | P1.3 补齐 |
| 9 | `AICallTrace` 有引用无定义 | P1.3 补齐 |
| 10 | `TraversalContext` 及相关类型未记录 | P1.3 补齐 |
| 11 | 异常层级未完整记录 | P1.3 补齐 |
| 12 | 工具方法未提及 | C# 中作为扩展方法自然包含在对应文件 |

---

## 附录 A：文件数量预估

| Phase | 模型/接口文件 | 实现文件 | 测试文件 | 合计 |
|-------|:---:|:---:|:---:|:---:|
| Phase 1 | ~49 | 0 | 0 | ~49 |
| Phase 2 | 0 | 5 | 5 | 10 |
| Phase 3 | 0 | 4 | 3 | 7 |
| Phase 4 | 0 | 3 | 0 | 3 |
| **总计** | **~49** | **12** | **8** | **~69** |

---

## 附录 B：Python `core-models.md` 同步原则

当 Python 端 `core-models.md` 更新时：
1. 检查是否影响 C# Domain 模型
2. 在 `feature/refactor` 分支对应文件同步修改
3. 保持两端的语义对齐（不追求字面对齐，C# 端按 C# 惯例表达）

---

**文档版本**: 1.0
**最后更新**: 2026-06-30
**维护者**: Uni-Claw 架构组
