## Why

三个子接口（TextUnderstanding / TraversalAdvisor / PageAnalyzer）各依赖一份 `analyze_visual` / `parse_instruction` / `decide_next_action` prompt 模板。当前每份模板在测试侧 inline 复制（5 处），无生产侧统一注册点。OQ-4 follow-up：创建一个 `PromptTemplateRegistry` 作为 3 模板的集中常量存放处，消除 inline 重复，为 L2 host 生产 DI wiring 提供单一入口。

## What Changes

- **新增** `src/UniClaw.Core/UniBrain/PromptTemplateRegistry.cs`：3 个 static `PromptTemplate` 属性（`ParseInstruction` / `DecideNextAction` / `AnalyzeVisual`），文本来自已归档 change `unibrain-analyzevisual-vertical-slice` design.md §4.1 终稿。
- **修改** 3 个测试文件（`TextUnderstandingTests` / `TraversalAdvisorTests` / `PageAnalyzerTests`）+ 2 个端到端测试文件：去掉 inline prompt 常量，引用 `PromptTemplateRegistry`。
- 零行为变化、零新 dependency、零接口签名改动。

## Capabilities

### New Capabilities

无（非新 capability，OQ-4 执行项）。

### Modified Capabilities

无（prompt 文本不变，仅搬移位置）。

## Impact

- 5 测试文件去除 inline 常量，引用 `PromptTemplateRegistry`
- prompt 模板单点真源：改 prompt 只需改 registry
- host 生产 DI wiring 可通过 registry 加载模板注册到 `PromptLibrary`
