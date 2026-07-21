# UniBrain — Unified AI Service Layer

> **状态**: 概念设计草案
> **日期**: 2026-07-21
> **作者**: Fran

## 动机

当前 `IVisionProvider` 只覆盖视觉分析（截图 → PageAnalysis），但真实遍历需要 AI 做三件事：

| 能力 | 当前 | 目标 |
|------|------|------|
| **Vision** | IVisionProvider.AnalyzeCurrentPageAsync | 截图 → PageAnalysis |
| **Text** | ❌ 无 | 文本理解、OCR 后处理、语义分类 |
| **Decision** | IAIStrategyAdvisor (仅骨架) | 策略选择、动作规划、上下文推理 |

三个能力共享同一组基础设施（模型调用、token 预算、重试、缓存），不应该每个能力一套独立接口。**UniBrain** 是对这三个能力的统一抽象。

## 概念设计

```
┌─────────────────────────────────────────┐
│              IUniBrain                   │
├─────────────────────────────────────────┤
│  AnalyzeScreenAsync()      → Vision     │
│  UnderstandTextAsync()     → Text       │
│  DecideAsync()             → Decision   │
└─────────────────────────────────────────┘
          │ 统一的模型调用 + 观测 + 重试
          ▼
┌─────────────────────────────────────────┐
│          IModelProvider (抽象)           │
├─────────────────────────────────────────┤
│  GPT-4o / Claude / Gemini / 本地模型    │
└─────────────────────────────────────────┘
```

### 三种能力

**1. Vision — `AnalyzeScreenAsync`**
- 输入: 截图 (byte[]/Stream) + 可选上下文 (上次分析结果、导航意图)
- 输出: PageAnalysis (结构化页面元素列表)
- 取代: 当前 IVisionProvider

**2. Text — `UnderstandTextAsync`**
- 输入: 文本字符串 + 上下文
- 输出: 结构化理解结果
- 场景: 检测到的文字后处理、语义分类、意图识别

**3. Decision — `DecideAsync<T>`**
- 输入: DecisionContext (当前状态、可选动作集、约束)
- 输出: 结构化的决策结果
- 场景: ErrorStrategy 选择、Popup 分类、下一步动作规划

### 可插拔后端

ModelProvider 层抽象不同的 AI 后端:

```
IUniBrain
  └── IModelProvider (接口)
        ├── OpenAiModelProvider  (GPT-4o)
        ├── AnthropicModelProvider (Claude)
        ├── LocalModelProvider   (Ollama/vLLM)
        └── MockModelProvider    (测试用)
```

UniBrain 实现本身不直接调用 API，委托给 `IModelProvider`。
`IModelProvider` 负责: 调用重试、token 预算、超时、观测记录（→ TraceCoordinator）。

### 与现有架构的关系

| 组件 | 关系 |
|------|------|
| `IVisionProvider` | **被 UniBrain.Vision 取代**（迁移路径: IVisionProvider → IUniBrain adapter） |
| `IAIStrategyAdvisor` | **被 UniBrain.Decision 取代** |
| `TraceCoordinator` | ModelProvider 层调用 `RecordAICallSpanAsync`（统一观测） |
| `Simulation/*` | `MockModelProvider` 替代现有 mock vision service |

### 非目标 (此文档不涉及)

- 具体模型选型（GPT-4o vs Claude vs 其他）
- Token 计费和预算策略
- prompt 工程模板
- 多模态模型的具体 schema 设计
- 平台适配层（Android ADB 截图 → IUniBrain）—— 那是平台项目的职责

## 下一步

1. 讨论接口形状: 统一 `IUniBrain` vs 三个独立接口 (IVision/IText/IDecision) 的组合
2. 决策 `IUniBrain` 在 Mode A/B 中的角色
3. 设计 `IModelProvider` 的抽象边界
4. 迁移路径: 从现有 IVisionProvider 到 IUniBrain
