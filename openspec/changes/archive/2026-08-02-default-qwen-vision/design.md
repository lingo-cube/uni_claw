## Context

当前视觉模型只有 Sensenova（日日新）和 Claude。Sensenova 单阶段截断率 61%，Claude 成本高。Qwen 3.7-plus 通过百炼平台 OpenAI 兼容端点接入，实测 0% 截断、16-48s 延迟、2-3.5k token——各方面优于 Sensenova。

端点 `https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1` 已有 `qwen3.7-plus`（视觉）、`deepseek-v4-flash-0731`（文本）等模型。二阶段方案可用 Qwen 做视觉提取 + DeepSeek 做文本推理，共享同一端点同一 API Key。

## Goals / Non-Goals

**Goals:**
- Qwen 作为一等视觉供应商，与 Sensenova/Claude 并列
- 环境变量驱动，不硬编码模型名
- 支持单阶段/二阶段通过 `UNICLAW_VISION_MODE` 切换
- 默认模型 `qwen3.7-plus`，无需 `--model` 参数

**Non-Goals:**
- 不实现 `TwoStagePageAnalyzer`（已有设计文档，本次只做 provider 层配置）
- 不改 Sensenova/Claude 现有行为
- 不改 `IPageAnalyzer` 接口

## Decisions

### 1. 复用 `OpenAiCompatibleVisionProvider`

Qwen 百炼端点完全兼容 OpenAI `/v1/chat/completions` 协议，无需新建 Provider 类。与 Sensenova 共用同一传输层，仅配置不同（baseUrl、model、apiKey）。

### 2. 二阶段用同一 API Key 注册两个 Provider

在 `UNICLAW_VISION_MODE=two_stage` 时，`CreateProviders` 返回两个条目：
- `"qwen"` → S1 视觉提取（`qwen3.7-plus`）
- `"deepseek"` → S2 文本推理（`deepseek-v4-flash-0731`）

共享同一 API Key 和 BaseUrl，仅 model 不同。

### 3. Model 默认值优先级

```
--model 参数 > UNICLAW_MODEL 环境变量 > QWEN_MODEL 环境变量 > "qwen3.7-plus"
```

Sensenova 也应有同样的默认值机制（当前强制要求 `--model`），本次不改。

### 4. `ProviderReady` 对 qwen 不检查 Model

因为 Qwen 有默认模型 `qwen3.7-plus`，`Model` 不再是非空约束。但 `Model` 的 CLI 解析 (`Parse()`) 中 `UNICLAW_MODEL` 仍是全局 fallback——如果用户设了 `UNICLAW_MODEL=qwen3.7-plus` 但同时用 `--provider sensenova`，会有混淆。这是现有行为，本次不改。

## Risks / Trade-offs

- **[Risk] Qwen API 限流或不可用** → 用户可 `--provider sensenova` 回退
- **[Risk] `UNICLAW_VISION_MODE=two_stage` 目前不生效** → 文档标注 "实验性，需 TwoStagePageAnalyzer 实现后启用"
- **[Risk] 百炼端点不可达（网络/账号）** → 超时 300s，与 Sensenova 一致
