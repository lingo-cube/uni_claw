## Context

UniClaw.Core 项目当前 AI 层由两个独立接口构成：`IVisionProvider`（5+4 方法，视觉分析 + 滚动感知）和 `IAIStrategyAdvisor`（骨架，零消费者）。两者职责重叠（VerifyPageType 在两处分裂）、粒度不当（滚动是设备状态查询，不是 AI 判断），且 IAIStrategyAdvisor 参数引用 ITraversalContext 导致 UniBrain↔StateMachine 双向依赖。

Python 侧已有 `UniBrain` 统一抽象（5 capability routing via YAML）+ `AIStrategyAdvisor` ABC + `AIProvider` base class。C# 侧需建立对齐的类型安全版本。

当前层级依赖：Domain(底层) → Graph → StateMachine → Traversal → Observability(cross-cutting)。UniBrain 新增层位于 Domain 上方，StateMachine/Traversal 注入 IUniBrain（acknowledged 向上引用，同 D-14/D-17 模式）。

PRD 来源: `docs/prd/2026-07-22-unibrain-prd.md`，经过 3 轮审阅修复（双向依赖、namespace 错误、字段对齐、观测责任归属等 13 项缺陷已全部修复/确认）。

## Goals / Non-Goals

**Goals:**
- 建立 `IUniBrain` unified facade，统一 AI 服务入口（页面感知+验证、遍历决策、文本理解）
- 通过 ISP 子接口（IPageAnalyzer / ITraversalAdvisor / ITextUnderstanding）实现各能力独立测试/替换/路由
- 配置驱动组合（UniBrainService 纯组合容器，无品牌 monolith），子接口实现独立可替换
- 将滚动感知分离到 `IScreenStateProvider`（Traversal namespace，独立于 AI）
- 消除 UniBrain↔StateMachine 双向依赖（ITraversalAdvisor 方法只用 Domain+BCL 类型）
- 对齐 Python UniBrain 接口和类型字段
- 新增 ArchitectureGuard tests 确保 UniBrain 零向上引用和接口形状锁定

**Non-Goals:**
- 具体模型选型（Claude vs DeepSeek vs 其他）— Host 项目职责
- Token 计费和预算策略
- Prompt 工程模板（子接口实现内联 prompt，PromptManager YAGNI）
- IModelProvider.SupportedModes（误配 guard，YAGNI，defer Phase 3）
- VerifyPageWithVisionAsync 实现（Host 层便利方法，不在 Core 接口）
- IScreenStateProvider 与 PageAnalysis 滚动数据桥接的具体方案选择（defer Phase 3-A 实施时）
- Host 项目具体实现（UniClaw.ClaudeProvider/DeepSeekProvider/Device — 需外部依赖 E-1/E-2 解锁）
- 本地模型（Ollama/vLLM）支持（Phase 3+）

## Decisions

### D-1: Hybrid facade + ISP（对外统一，内部独立）

**决策**: 对外统一 `IUniBrain` facade（单一注入点），内部 3 子接口各自独立（ISP）。

**理由**: 消费者注入一个东西，但各能力可独立测试/替换/路由到不同 provider。纯统一接口（3 方法都在 IUniBrain 上）牺牲 ISP；纯独立接口（3 个分别注入）增加注入复杂度。Hybrid 兼顾两者。

**替代**: 纯统一接口（IUniBrain 3 方法）→ ISP 损失；纯独立接口（分别注入）→ 注入复杂。

### D-2: 子接口按职责语义分组，非按调用模式

**决策**: IPageAnalyzer（页面感知+验证）、ITraversalAdvisor（遍历决策）、ITextUnderstanding（文本理解）。

**理由**: 旧 Vision/Text/Decision 分组本质是按 AI 调用模式（需要截图/纯文本/需上下文），导致职责混乱：IVisionBrain 混了 4 种职责，IDecisionBrain 混了 5 种，VerifyPageTypeAsync 在两处分裂。按职责分组：每个接口单一职责，内聚性高。

**替代**: Vision/Text/Decision 三分组 → 职责混乱已验证；单一 IUniBrain → ISP 损失。

### D-3: IUniBrain 替换 IVisionProvider

**决策**: TraversalEngine/StepContext 注入 IUniBrain 而非 IVisionProvider。

**理由**: 统一 AI 服务入口，避免引擎同时注入 IVisionProvider + IAIStrategyAdvisor 两个 AI 接口。Mode A/B 成为 IPageAnalyzer 实现选择（ClaudePageAnalyzer / RuleBasedPageAnalyzer），facade 无感。

### D-4: 滚动感知脱离 AI — IScreenStateProvider 独立

**决策**: 滚动方法从 IVisionProvider 分离到 `IScreenStateProvider`（Traversal namespace），不在 IUniBrain 上。

**理由**: 滚动是设备/平台状态查询，不是 AI 判断。Simulation mock 返回编程值不走 AI。Mode A: AI 在 PageAnalysis 中返回滚动字段；Mode B: 规则引擎推导。强制放 "大脑" 接口是职责泄漏。

### D-5: 配置驱动组合，非品牌 monolith

**决策**: 无 `ClaudeUniBrain` 品牌绑定类。`UniBrainService` 是纯组合容器（sealed class），子接口实现独立可替换，组合由配置/DI 决定。

**理由**: 高内聚低耦合 — 每个子接口实现只关心自己的能力。品牌绑定在具体实现内部，不在 facade 上。配置灵活组合: Claude(vision) + DeepSeek(decision) + local(text) 等。

**替代**: ClaudeUniBrain 品牌类 → 紧耦合；单一 UniBrainService 含路由逻辑 → 职责过多。

### D-6: UniBrain 零 StateMachine 引用

**决策**: ITraversalAdvisor 方法只接收 Domain 类型 + BCL 类型，不引用 ITraversalContext（StateMachine 接口）。

**理由**: 避免 UniBrain↔StateMachine 双向依赖。ITraversalContext 是 StateMachine 接口，如果 UniBrain 引用它，形成循环：StateMachine→UniBrain（注入）+ UniBrain→StateMachine（参数）。call site 从 ITraversalContext 提取 string/int 值直接传入，类型安全且解耦。

### D-7: 观测记录责任归属子接口实现

**决策**: 子接口实现调用 ITraceRecorder.RecordAICallAsync，将 capability 语义 + ModelResponse 数据合并写入 AICallRecord。IModelProvider 是纯传输层（call + retry + timeout），不负责观测记录。

**理由**: IModelProvider 不知道调用目的是 "page_analysis" 还是 "next_action"，只有子接口实现同时拥有 capability 语义和 ModelResponse 数据。

### D-8: UniBrain 层级归属 — Domain 上方

**决策**: UniBrain namespace (`UniClaw.Core.UniBrain`) 依赖 Domain.Content + Domain.Common，不依赖 StateMachine/Traversal。

**理由**: 保持层级依赖方向一致性。StateMachine/Traversal 注入 IUniBrain 是向上引用（acknowledged, 同 D-14/D-17），但 UniBrain 不反向引用，消除双向依赖。

## Risks / Trade-offs

- **[Risk] 向上引用 StateMachine→UniBrain + Traversal→UniBrain** → Acknowledged, 同 D-14/D-17 模式。ArchitectureGuard test 确认 UniBrain 零反向引用，双向依赖不可能形成。
- **[Risk] IScreenStateProvider 与 PageAnalysis 滚动数据桥接缺口** → PRD §2.8 记录 3 种方案，defer 具体选择到 Phase 3-A 实施时。推荐方案 B（PageAnalysisAwareScreenStateProvider 缓存桥接）。
- **[Risk] IVisionProvider 消费代码迁移面广** → 8 处 call site 需迁移（ctx.Vision → ctx.Brain.PageAnalyzer + ctx.ScreenState），逐个迁移，每步验证测试全绿。
- **[Risk] AI/ 目录删除后旧 namespace 引用残留** → ArchitectureGuard test 验证无残留引用；迁移完成后删除旧文件，编译失败即暴露。
- **[Trade-off] Hybrid facade 增加一层间接** → 消费者代码 `ctx.Brain.PageAnalyzer.AnalyzeCurrentPageAsync()` 比 `ctx.Vision.AnalyzeCurrentPageAsync()` 多一层属性访问。但 ISP 和可替换性收益远大于间接成本。
- **[Trade-off] UniBrainService sealed class（非 record）** → 服务容器不做值语义比较，sealed class 同 TraversalRuntimeContext 例外模式。

## Migration Plan

### Phase 3-A 实施顺序（8 步）

1. **新建 UniBrain/ 目录 + 接口定义 + 类型迁入**: 创建 15+ 文件，接口+类型先空壳再填充
2. **IScreenStateProvider 分离**: 从 IVisionProvider 提取 4 滚动方法到独立接口
3. **StepContext 改造**: `IVisionProvider Vision` → `IUniBrain Brain` + `IScreenStateProvider ScreenState`
4. **ArchitectureGuard tests**: 6 新增 guard tests
5. **Mock 组合迁移**: StatefulMockVisionService → MockPageAnalyzer + MockScreenStateProvider
6. **引擎消费代码迁移**: Traversal + StateMachine call sites 逐个迁移
7. **删除旧 AI/ 目录 + IVisionProvider**: 清空旧 namespace
8. **Host 项目骨架**: UniClaw.ClaudeProvider/DeepSeekProvider/Device（需外部依赖解锁）

### 回滚策略

每步独立可回滚：Step 1-3 新增代码不影响旧代码（双接口并存期）；Step 6 消费代码迁移逐个文件；Step 7 删除旧代码是最后一步。任何步骤失败可 git revert 该步骤的 commit。

### 双接口并存期

Step 1-5 完成后，IVisionProvider 和 IUniBrain 并存。Step 6 逐步迁移消费代码。Step 7 删除 IVisionProvider 时所有消费代码已迁移完毕。
