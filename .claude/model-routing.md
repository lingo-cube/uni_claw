# Model Routing — 单点真源

> **模型路由配置的唯一权威。改模型路由只改本文件，不改任何 agent 定义。**
> agent 定义只声明类型档位（frontmatter `model` 为平台枚举），正文不再写背后路由。
> 2026-08-06 用户拍板：外挂映射 + 只分类型。

## 类型体系

| 类型 | 档位 (frontmatter) | 用途 | 背后路由 | 归属 agent |
|------|-------------------|------|---------|-----------|
| **leader** | fable (glm-5.2[1M]) | 顶层统筹（主会话主循环，不额外开统筹子代理） | — | 主会话 |
| **expert** | opus | 攻坚/决策密集（跨模块重构、深度故障定位、方案权衡） | glm-5.2 → deepseek-v4-pro（降级链，见下） | openspec-refactorer |
| **standard** | sonnet | 常规编码 + 领域分析 + 场景设计（5 个 agent 同档） | deepseek-v4-flash | openspec-coder, scenario-architect, fsm-analyzer, shadow-fsm-analyzer, trace-analyzer, local-vision-analyzer |
| **fast** | haiku | 轻量只读（检索/日志/探查） | deepseek-v4-flash | openspec-researcher |

> **expert 降级链**（代理层真源 `tier_routes.opus`）：`glm-5.2 (qwen-anthropic)` → `qwen3.7-max` → `deepseek-v4-pro (deepseek)`。表内值填「主路由 → 末位降级」，完整链以代理层配置为准。

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

1. 只改本表「背后路由」列
2. 验证：实际调用模型 = 表内值（trace-analyzer 可从 run.log `ai_call` 记录核对）
3. 代理层配置变更时同步本表

## 历史

- **2026-08-06**: 外挂映射建立。此前路由散落在 opsx/AGENT.md 表格 + 各 agent 定义正文（coder 写 deepseek-v4-pro 与 AGENT.md 的 flash 矛盾；Fable 描述在 AGENT.md/apply.md/propose.md 三处重复）。
- **2026-08-06**: standard 档位验证通过（代理层 `tools/litellm-bar/config/providers.json` `tier_routes.sonnet`：主路由 sensenova → `openai/deepseek-v4-flash`，fallback 为 flash-0731 / deepseek-v4-flash）——表内值 flash 正确，coder 曾写的 v4-pro 确认错误（v4-pro 仅属 opus 档位），⚠️ 移除。
- **2026-08-06**: expert 档位降级处理——代理层真源 `tier_routes.opus` 主路由为 qwen-anthropic → glm-5.2，v4-pro 仅为第三位降级，表内值由单值 deepseek-v4-pro 修正为「glm-5.2 → deepseek-v4-pro」降级链表达。
