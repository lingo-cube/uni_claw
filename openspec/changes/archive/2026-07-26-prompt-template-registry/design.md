## Context

OQ-4 follow-up from `unibrain-analyzevisual-vertical-slice` (已归档)。当前 3 份 prompt 模板在各测试文件 inline：`TextUnderstandingTests` / `TraversalAdvisorTests` / `PageAnalyzerTests` 各一份 SystemPrompt + UserPrompt 常量，`ParseInstructionEndToEndTests` / `DecideNextActionEndToEndTests` / `AnalyzeVisualEndToEndTests` 又各一份。共 6 处复制。prompt 文本已在各自 change 的 design.md 终稿锁定。

## Goals / Non-Goals

**Goals:**
- 新增 `PromptTemplateRegistry` 集中存放 3 份生产 prompt 模板
- 5 测试文件从 inline 常量改为引用 registry
- prompt 改版只改一处

**Non-Goals:**
- 不改 prompt 文本语义（零行为变化）
- 不涉及 host 生产 DI wiring（属另一 change）

## Decisions

### D1: static 属性而非 DI 注册

**选择**：`PromptTemplateRegistry` 为 `static` 类，3 个 `public static PromptTemplate` 只读属性。测试直接引 `PromptTemplateRegistry.AnalyzeVisual`。

**理由**：prompt 模板是编译期常量（不改不重启），无 DI 必要。static 属性零开销、零装配期、测试直接引用无需 mock。若未来需支持多语言/多版本切换，再改为 DI 注册。

### D2: 模板文本权威来源

**选择**：`AnalyzeVisual` 文本来自 `unibrain-analyzevisual-vertical-slice` design.md §4.1 终稿（已锁定，§12-A 剥散文后版本）。`ParseInstruction` / `DecideNextAction` 文本来自各测试文件 inline 常量（即原 design 终稿）。

## Risks / Trade-offs

- 无（纯搬移，零行为变化，回滚 = 删除 registry + 恢复 inline 常量）。

## Migration Plan

无迁移。纯新增 registry + 5 测试文件改引用。
