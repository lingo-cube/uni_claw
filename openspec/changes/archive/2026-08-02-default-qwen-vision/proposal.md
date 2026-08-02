## Why

Sensenova 6.7-flash-lite 视觉模型在单阶段模式下有 **61% 截断率**，导致生产遍历频繁失败。经 6 页面 × 18 次调用的对照实验，Qwen 3.7-plus 实现 **0% 截断**、延迟降低 35%（16-48s vs 25-100s）、token 节省 40%（2-3.5k vs 4.5-10k）。应默认接入 Qwen，并支持配置在多方案间切换。

## What Changes

- 新增 `qwen` 视觉供应商，复用 `OpenAiCompatibleVisionProvider`（端点兼容 OpenAI 协议）
- 支持 `UNICLAW_VISION_MODE=single|two_stage` 切换单阶段/二阶段策略
- 二阶段模式下自动注册 deepseek 作为 S2 文本推理模型
- 新增环境变量 `QWEN_API_KEY`、`QWEN_MODEL`、`QWEN_BASE_URL`、`DEEPSEEK_MODEL`
- `--provider qwen` 不强制要求 `--model`（默认 `qwen3.7-plus`）
- Provider 就绪检查排除 `Model` 非空约束（有默认值）

## Capabilities

### New Capabilities

- `qwen-vision-provider`: Qwen 3.7-plus 作为视觉模型接入，支持单阶段/二阶段模式，环境变量配置，与现有 Sensenova/Claude provider 并存

### Modified Capabilities

- `unibrain-facade`: `UniBrainFactory` 支持 `UNICLAW_VISION_MODE` 切换单阶段/二阶段 PageAnalyzer；`CreateProviders` 在 qwen + two_stage 模式下注册两个 provider（qwen 视觉 + deepseek 文本）

## Impact

- `src/UniClaw.Host/Commands/HostCommands.cs` — provider 注册、API key 加载、Model 默认值、ProviderReady 条件
- `src/UniClaw.Core/UniBrain/UniBrainFactory.cs` — 二阶段分支（需 TwoStagePageAnalyzer 实现）
- `~/.litellm/secrets.json` — 新增 `QWEN_API_KEY`
- 向后兼容：默认 provider 仍为 claude，sensenova 路径不变
