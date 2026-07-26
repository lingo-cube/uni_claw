## 1. PromptTemplateRegistry

- [x] 1.1 新建 `src/UniClaw.Core/UniBrain/PromptTemplateRegistry.cs`：`public static class`，3 个 `public static PromptTemplate` 属性（`ParseInstruction` / `DecideNextAction` / `AnalyzeVisual`）。`AnalyzeVisual` 文本来自已归档 `unibrain-analyzevisual-vertical-slice` design.md §4.1 终稿（§12-A 剥散文后版本，Variables 空）。`ParseInstruction` / `DecideNextAction` 文本来自 `TextUnderstandingTests` / `TraversalAdvisorTests` 内联常量。

## 2. 测试引用迁移

- [x] 2.1 `TextUnderstandingTests`：`MakePromptLibrary()` 从 inline `new PromptTemplate(...)` 改为 `new PromptLibrary(PromptTemplateRegistry.ParseInstruction)`
- [x] 2.2 `TraversalAdvisorTests`：同上改为 `PromptTemplateRegistry.DecideNextAction`
- [x] 2.3 `PageAnalyzerTests`：`MakePromptLibrary()` 改为 `PromptTemplateRegistry.AnalyzeVisual`
- [x] 2.4 `ParseInstructionEndToEndTests` / `DecideNextActionEndToEndTests` / `AnalyzeVisualEndToEndTests`：分别引用对应 registry 属性

## 3. 验证

- [x] 3.1 `dotnet build src/UniClaw.Core.sln`：0 错误
- [x] 3.2 `dotnet test src/UniClaw.Core.sln`：930/930 全绿
