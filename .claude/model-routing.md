# Claude Model Routing Adapter

> Claude Code 适配层。
> 跨 Codex / Claude 的 portable role map 看 `.ai/agent-routing.md`。
> 背后 provider / fallback 配置看 `.ai/model-routing.yaml`。
> agent 定义只声明 Claude 平台档位（frontmatter `model` 为 `opus` / `sonnet` / `haiku`），正文不写背后路由。

## 类型体系

| 类型 | 档位 (frontmatter) | 用途 | 背后路由 | 归属 agent |
|------|-------------------|------|---------|-----------|
| **leader** | fable (main session) | 顶层统筹（主会话主循环，不额外开统筹子代理） | `.ai/model-routing.yaml` `tiers.leader` | 主会话 |
| **expert** | opus | 攻坚/决策密集（跨模块重构、深度故障定位、方案权衡） | `.ai/model-routing.yaml` `tiers.expert` | openspec-refactorer |
| **standard** | sonnet | 常规编码 + 领域分析 + 场景设计 + 独立验收 | `.ai/model-routing.yaml` `tiers.standard` | openspec-coder, scenario-architect, runtime-coder, runtime-evolution-agent, runtime-validator |
| **fast** | haiku | 轻量只读（检索/日志/探查） | `.ai/model-routing.yaml` `tiers.fast` | openspec-researcher |

> 完整 provider / fallback 链以 `.ai/model-routing.yaml` 为准；本文件只说明 Claude frontmatter 档位如何映射到共享 tier。

## 派发规则（与档位类型绑定）

| 子任务类型 | Agent 类型 | 档位类型 |
|-----------|-----------|---------|
| 文件检索、日志解析、正则校验、信息探查 | `openspec-researcher` | fast |
| Scenario 设计、架构验证、Fake World 设计、最小 Vocabulary 推导 | `scenario-architect` | standard |
| 常规功能编码、普通 Bug 修复、单元测试、接口实现 | `openspec-coder` | standard |
| 跨模块重构、复杂流程梳理、深度故障定位、方案决策 | `openspec-refactorer` | expert |

## 降级链路（仅顶层统筹）

`Fable → Opus`（止步，禁止继续落到 standard/fast 承担顶层规划）。Opus 也异常时向用户告警，等待人工介入。

## 路由变更流程

1. 改 agent 角色/职责：先改 `.ai/agent-routing.md`
2. 改模型 provider / fallback：先改 `.ai/model-routing.yaml`
3. 改 Claude 平台枚举：只在必须换 `opus` / `sonnet` / `haiku` 档位时改 `.claude/agents/*.md`
4. 验证：实际调用模型 = `.ai/model-routing.yaml` 对应 tier（可从 run.log `ai_call` 记录核对）

## 历史

- **2026-08-06**: 外挂映射建立。此前路由散落在 opsx/AGENT.md 表格 + 各 agent 定义正文（coder 写 deepseek-v4-pro 与 AGENT.md 的 flash 矛盾；Fable 描述在 AGENT.md/apply.md/propose.md 三处重复）。
- **2026-08-06**: standard 档位验证通过（代理层 `tools/litellm-bar/config/providers.json` `tier_routes.sonnet`：主路由 sensenova → `openai/deepseek-v4-flash`，fallback 为 flash-0731 / deepseek-v4-flash）——表内值 flash 正确，coder 曾写的 v4-pro 确认错误（v4-pro 仅属 opus 档位），⚠️ 移除。
- **2026-08-06**: expert 档位降级处理——代理层真源 `tier_routes.opus` 主路由为 qwen-anthropic → glm-5.2，v4-pro 仅为第三位降级，表内值由单值 deepseek-v4-pro 修正为「glm-5.2 → deepseek-v4-pro」降级链表达。
