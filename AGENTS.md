# AGENTS.md — UniClaw Development Harness（uni-harness 分支）

> 本分支是 UniFlow V2 Development Harness 的 canonical 落点。所有
> OpenAI/Codex-compatible AI Coder（Codex / DSH / 其他 Host）以本文件为唯一
> 持久开发约束入口；Host 差异只存在于各自 adapter，不得在此定义。
> 本文件只保存稳定约束；流程细节、字段定义、清单内容一律指向唯一真相源。

## 1. 定位与边界

- 本分支只承载 Development Harness（流程 + Skill + 契约），不承载产品代码；
  产品架构基线在 `docs/analysis/`（仅参考）。
- Canonical surface（OpenAI/Codex 原生约定）：`AGENTS.md`（本文件）、
  `.agents/skills/<name>/SKILL.md`、`schemas/`、`uniflow.md`、`model-routing.yaml`。

## 2. 真相在哪（Where Is Truth）

| 需要什么 | 唯一真相源 |
|---|---|
| 开发流程与分级（WHEN / WHAT NEXT） | `uniflow.md` |
| Skill 清单与 provenance | `.agents/skills/` + `skills-lock.json` |
| WorkItem 契约（字段级定义） | `schemas/work-item.schema.json` |
| 模型路由（capability → tier） | `model-routing.yaml`（provider 绑定只在 adapter） |
| 架构决策 | `docs/adr/`（上游 ADR 约定） |
| 计划 / 任务 / 证据 | `plans/` · `workitems/` · `evidence/` |

UniFlow 回答 WHEN / WHAT NEXT；**Skill 只回答 HOW**，不得拥有第二套生命周期。

## 3. Skills（vendored upstream）

- 物理位置 `.agents/skills/`（skills installer 标准输出；Codex 与 DSH 均直接
  消费，已实测）。已批准清单以 `skills-lock.json` 为准，不在此复制枚举。
- 第三方 Skill 保持上游原文不改；本地需求经 UniFlow 组合 / 本地扩展 /
  adapter / reference 解决；fork 必须记录 upstream base / reason / delta。
- 安装：`npx skills@latest add <owner>/<repo> -s <name> -a codex -y --copy`
  （`-s` 逐个重复传）；更新：`npx skills@latest update`。
- Skill 不控制 UniFlow、不创建第二套 task system、不绑定具体模型（只可声明
  required capability）。

## 4. 执行纪律（稳定约束）

- WorkItem **self-contained, not self-bloated**：只回答做什么/为什么/在哪里/
  什么不能碰/完成标准/去哪读更多（anchors、contract_refs 按需加载）；
  `role_profile` 等治理字段 optional，可推导时不得强制。
- 默认 Fresh Context：One WorkItem = One Disposable Execution Context；失败
  先修 WorkItem 信息与持久化，不是延长 Session（`uniflow.md` §5）。
- **Worker 自述 ≠ Completion Evidence**：完成只由 Evidence + acceptance 判定，
  且判定不得依赖「这是 Codex 还是 DSH」。
- 调试硬规则见 `uniflow.md` §1（No reliable RED → No fix；No FDP / Owner →
  No implementation WorkItem；No RED → GREEN regression → Not proven fixed）。
- 实现默认 TDD（RED → 最小实现 → GREEN → Refactor）；高风险 WorkItem 用
  Fresh Review Context。

## 5. 禁止事项

- 重新引入 OpenSpec 生命周期，或因弃用它另造同重量的 Spec 系统。
- Codex / DSH 各维护一份 Skill 源或 WorkItem schema（含 `DSH_AGENTS.md`、
  `dsh-skill.md`、`dsh-task.json` 式副本）。
- Host 专有 session/tool/transport 语义进入共享层（`uniflow.md`、
  `.agents/skills/`、`schemas/`）。
- 一次加载全部 Skills；把完整历史上下文塞给 Worker；依赖旧 Session 才能继续。
- 在本分支混入产品代码，或把 Product Architecture 与 Development Harness
  混在一起。

## Agent skills

### Issue tracker

Work is tracked as UniFlow WorkItems in `workitems/` (one JSON per item, schema
`schemas/work-item.schema.json`). See `docs/agents/issue-tracker.md`.

### Domain docs

Single-context: `CONTEXT.md` at the repo root (created lazily by
`/domain-modeling`); decisions live as ADRs in `docs/adr/`. See
`docs/agents/domain.md`.
