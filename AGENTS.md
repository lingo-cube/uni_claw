# AGENTS.md — UniClaw Development Harness（uni-harness 分支）

> 本分支是 UniFlow V2 Development Harness 的 canonical 落点。所有
> OpenAI/Codex-compatible AI Coder（Codex / DSH / 其他 Host）以本文件为唯一
> 持久开发约束入口；Host 差异只存在于各自 adapter，不得在此定义。
> 本文件只保存稳定约束；流程细节、字段定义、清单内容一律指向唯一真相源。

## 1. 定位与边界

- 本分支是重构分支：承载 Development Harness（流程 + Skill + 契约）与
  GREENFIELD 目标产品代码（Target v0.1 谱系，落 `src/UniClaw.*` / `tests/`）。
  旧产品谱线在 `uni-agent` 分支，仅参考，不合并、不依赖。
- 产品架构基线：`docs/architecture/product-architecture-baseline-l0-l3.md`
  （Target v0.1，L0-L3 CLOSED；对 GREENFIELD 产品代码为直接权威）。
- Canonical surface（OpenAI/Codex 原生约定）：`AGENTS.md`（本文件）、
  `.agents/skills/<name>/SKILL.md`（含 `uniflow` 控制面本体）、`schemas/`、
  `model-routing.yaml`。

## 1.5 开发原则（2026-09-20 所有者补充；对所有工作生效）

1. **一切行为遵循架构原则**：已接受的 baseline / ADR / 协议基线优先于
   实现便利；冲突时停下裁决，不绕行。
2. **边界清晰、职责分明**：每个组件单一职责；跨组件只经显式缝
   （接口 / 协议 / 投影），禁止绕缝直连或跨层伸手。
3. **高内聚低耦合**：组件内聚为模块，组件间只依赖抽象缝；缝的实现
   可替换（仿真 ⇄ 真件），替换不改核心。
4. **验收标准明确**：每个 change 的 Acceptance 先于实现可判定，完成以
   四元组（method/expected/actual/evidence）证明，不以「写完了」为完成。
5. **基础组件与服务求「完备且可扩展」**：完备 = 真实能力所需的缝全部
   就位且有执法；可扩展 = 新能力经「实现既有缝」接入，而非修改核心。
   缝的形状由真实 buyer 的证据验证后冻结（ADR-0026），不为想象中的需求预造。

## 2. 真相在哪（Where Is Truth）

| 需要什么 | 唯一真相源 |
|---|---|
| 开发流程与执行纪律（8 状态主干 / 入口协议 / 失败转移 / 上下文经济学 / 委派协议 / 完成判定） | `.agents/skills/uniflow/SKILL.md` |
| Change State（WHAT/WHY/ACCEPTANCE，三档深度） | `changes/`（规范见 `changes/README.md`） |
| Skill 清单与 provenance | `.agents/skills/` + `skills-lock.json` |
| WorkItem 派发协议（Leader→SubAgent，仅委派工作；字段级契约） | `schemas/work-item.schema.json`（载荷按需落 `workitems/`） |
| 模型路由（capability → tier） | `model-routing.yaml`（provider 绑定只在 adapter） |
| 架构决策 | `docs/adr/`（上游 ADR 约定） |
| 计划 / 证据 | `plans/` · `evidence/` |

UniFlow 回答 WHEN / WHAT NEXT；**Skill 只回答 HOW**，不得拥有第二套生命周期。

## 3. Skills（vendored upstream）

- 物理位置 `.agents/skills/`（skills installer 标准输出；Codex 与 DSH 均直接
  消费，已实测）。已批准清单以 `skills-lock.json` 为准，不在此复制枚举。
- 第三方 Skill 保持上游原文不改；本地需求经 UniFlow 组合 / 本地扩展 /
  adapter / reference 解决；fork 必须记录 upstream base / reason / delta。
- 安装：`npx skills@latest add <owner>/<repo> -s <name> -a codex -y --copy`
  （`-s` 逐个重复传）；更新：`npx skills@latest update`。
- Skill 不控制 UniFlow、不创建第二套 task system、不绑定具体模型（只可声明
  required capability）。唯一例外：`uniflow` 本身——控制面以 LOCAL_UNIFLOW
  skill 形式驻留标准位置（ADR-0005）。

## 4. 禁止事项

- 重新引入 OpenSpec 生命周期，或因弃用它另造同重量的 Spec 系统。
- Codex / DSH 各维护一份 Skill 源或 WorkItem schema（含 `DSH_AGENTS.md`、
  `dsh-skill.md`、`dsh-task.json` 式副本）。
- Host 专有 session/tool/transport 语义进入共享层（`.agents/skills/`、`schemas/`）。
- 一次加载全部 Skills；把完整历史上下文塞给 Worker；依赖旧 Session 才能继续。
- 产品实现与 Harness 互相渗透：产品代码进入 Harness 机制层（`.agents/`、
  `schemas/`、`model-routing.yaml`、本文件），或把流程机制混入产品代码
  （`src/` / `tests/`）；产品/Harness 的分离按结构层维护，不按分支。
- GREENFIELD 产品代码依赖或合并 legacy（`uni-agent`）运行时。

## Agent skills

### Issue tracker

No standing issue tracker. Change State lives in `changes/` (durable,
to-spec destination); WorkItems live in `workitems/` (transient delegation,
to-tickets destination). See `docs/agents/issue-tracker.md` for the full
tool-to-surface mapping (to-spec → PERSIST, to-tickets → PLAN).

### Domain docs

Single-context: `CONTEXT.md` at the repo root (created lazily by
`/domain-modeling`); decisions live as ADRs in `docs/adr/`. See
`docs/agents/domain.md`.
