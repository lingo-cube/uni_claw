# Stop That Shit — 双 Harness 消费与执法现状

第三方 Skill（upstream `lennney/stop-that-shit`）：完成请求的工作，同时阻止
投机防御与范围蔓延。本仓库以 skills installer 标准形态 vendored，属
canonical surface（AGENTS.md §3），DSH 与 Codex 均直接消费。

## 仓库内落点

| 层 | 位置 | 状态 |
|---|---|---|
| Skill 本体 | `.agents/skills/stop-that-shit/SKILL.md` | 已提交 |
| OpenAI/Codex 接口清单 | `.agents/skills/stop-that-shit/agents/openai.yaml` | 已提交（default_prompt = `$stop-that-shit review -- …`；`allow_implicit_invocation: true`） |
| 批准清单 | `skills-lock.json` → `stop-that-shit` | 已登记（source/computedHash），`npx skills@latest update` 走标准更新链路 |

同批安装的 `lucid`（upstream `ustas-eth/lucid`）同属本提交；两者是同一次
lock 状态变更，拆开提交会破坏 lock 完整性。

## 调用方式

- **DSH**：会话技能目录按需加载（触发条件：额外加固、过度工程、重复验证环、
  越界；或用户点名）。
- **Codex / host-neutral**：任务首行指令合同——
  `$stop-that-shit change -- <任务>` / `review` / `monitor`；
  文件锁 `lock change files=a|b`；委派限额 `agents=N`（`0` 禁止新委派）。
  查询命令（不改合同）：`status` / `runtime` / `explain evt_…` / `label …`。

## 执法现状（如实）

- Skill 层为 **advisory**：无 Guard 时不能保证模型行为，只约束指令遵守。
- **Guard 机器执法未安装**：本仓库与 `~/.codex/` 均无安装证据；如后续安装，
  其默认为 `unconfirmed` 观察模式（只记录不拦截），需显式 arm 才生效。
  在此之前，任何"被 Guard 拦截"的声明都不成立。

## 边界（与 AGENTS.md §4 一致）

- UniFlow 路由表不引用本 Skill；它不创建第二套 task system、不绑定模型、
  不拥有生命周期——只回答 HOW。
- 使用本 Skill 不改变 Change State / WorkItem / 验证声明的既有语义。
