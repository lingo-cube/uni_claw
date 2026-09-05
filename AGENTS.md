# AGENTS.md — UniClaw Development Harness（uni-harness 分支）

> 本分支是 UniFlow V2 Development Harness 的 canonical 落点：工作流、Skill、
> WorkItem 契约、模型路由与持久工件约定。
> 所有 OpenAI/Codex-compatible AI Coder（Codex / DSH / 其他 Host）在本分支工作时
> 以本文件为唯一持久开发约束入口；Host 差异只能存在于各自 adapter，不得在此定义。

## 1. 定位与边界

- 本分支只承载 **Development Harness**（流程 + Skill + 契约），不承载产品代码。
- 产品架构基线文档在 `docs/analysis/`（4 份基线 + 3 份 V1 迁移调查记录，仅参考）。
- Canonical authoring surface 以 OpenAI/Codex 原生约定为准：
  `AGENTS.md`（本文件）、`.agents/skills/<name>/SKILL.md`、`schemas/`、
  `uniflow.md`、`model-routing.yaml`。

## 2. UniFlow — 唯一 Workflow 控制面

定义见 `uniflow.md`。阶段：

```text
Intent → Explore → Decision → Plan → ToWorkItems → Route
→ Execute（TDD / Diagnose / Implement）→ Review → Verify → Complete
```

UniFlow 回答 WHEN / WHAT NEXT（现在哪个阶段、下一步、是否需要 WorkItem、
依赖是否满足、是否存在真实 Human Gate、Evidence 是否足够完成）。
**Skill 只回答 HOW。** Skill 不得拥有第二套生命周期。

## 3. Skills — 唯一 canonical 源

物理位置 `.agents/skills/<name>/`（vercel-labs `skills` installer 的项目级
标准输出；Codex 与 DSH 均已实测从此发现）。第一批 allowlist（全部上游原文）：

```text
mattpocock/skills:
  setup-matt-pocock-skills · grilling · grill-with-docs · domain-modeling
  · codebase-design · tdd · code-review · diagnosing-bugs
humanlayer/skills:
  show-me
```

规则：

- 第三方 Skill 是 vendored dependency：**保持上游 SKILL.md 原文不改**。本地
  需求通过 UniFlow 组合、本地扩展、adapter 或 reference 解决；真实不兼容才
  fork，fork 必须记录 upstream base / reason / delta。
- Provenance 由 installer 的 `skills-lock.json` 承载（source / skillPath /
  computedHash）；不发明新 provenance 格式。
- 安装：`npx skills@latest add <owner>/<repo> -s <name> -a codex -y --copy`
  （`-s` 逐个重复传，不接受逗号串）；更新：`npx skills@latest update`。
- Skill 不控制 UniFlow、不创建第二套 task system、不决定 Model Routing；
  这些仓库级约束写在本文件，不写进上游 Skill 正文。
- Skill 只可声明 `required capability`，不绑定具体模型。
- 禁止 Codex Skills 与 DSH Skills 两套源文件。

## 4. WorkItem — 核心 Execution Protocol

唯一 schema：`schemas/work-item.schema.json`。

- 核心字段：`id / objective / scope / semantic_brief / anchors / contract_refs /
  acceptance / forbidden / frozen_decisions / dependencies`。
- `role_profile / execution_profile / module_profile / worker_owner` 为 optional，
  可自动推导时不得强制填写。
- WorkItem 是「一个 Fresh Agent 能独立理解、独立执行、独立验证的最小开发单元」：
  **Self-contained, but not self-bloated**。禁止默认附带完整架构文档、完整历史
  decisions、完整代码树或旧会话。

## 5. Model Routing

`model-routing.yaml`：`capability → tier → adapter → concrete model`。

- tier 与 capability 映射在共享层维护；具体 provider/model 绑定只存在于各
  Host adapter 配置（Codex / DSH），不进入共享文件。
- 禁止 silent downgrade。

## 6. Durable Artifacts（Artifacts carry state, sessions do not）

```text
decisions/   冻结决策（Frozen Decisions 的持久层）
plans/       Plan 工件（架构计划 / 模块设计 / WorkItem DAG）
workitems/   WorkItem 实例（含状态与依赖）
evidence/    完成证据（验证输出、评审结论、复现记录）
```

任何重要知识若只存在于 Conversation 中，视为尚未持久化。

## 7. 验证与完成（Evidence proves completion）

- **Worker 自述 ≠ Completion Evidence**。完成只由 Evidence + acceptance 判定，
  且判定不得依赖「这是 Codex 还是 DSH」。
- 调试强制（`diagnosing-bugs`）：
  `No reliable RED → No fix`；`No FDP / Owner → No implementation WorkItem`；
  `No RED → GREEN regression → Not proven fixed`。
- 实现默认 TDD（`tdd`）：RED → 最小实现 → GREEN → Refactor。
- 高风险 WorkItem 使用 Fresh Review Context（`code-review`）。

## 8. 禁止事项

- 重新引入 OpenSpec 生命周期（propose/apply/archive ceremony）。
- 建立 Codex / DSH 两套 Skill 源或两套 WorkItem schema。
- Host 专有 session/tool/transport 语义进入 `uniflow.md`、`.agents/skills/`、`schemas/`。
- 一次加载全部 Skills；把完整历史上下文塞给 Worker。
- 依赖旧 Session 才能继续开发（Fresh Context 是默认，见 `uniflow.md` §5）。
- 把 Product Architecture 与 Development Harness 混在一起。

## 9. 变更分级（进入流程前先分级）

| 级别 | 流程 |
|---|---|
| Small | Explore → Direct WorkItem → Execute → TDD/Verify → Complete |
| Medium | Explore → Decision/Plan → WorkItems → Fresh Context Execution → Review → Verify → Complete |
| Large/Architecture | Explore → Domain Modeling/Research/Prototype → Frozen Decisions → Architecture Plan → WorkItem DAG → Fresh Context per WorkItem → Independent Review/Verification → Complete |

不要让 Small Change 被迫经过 Large Change ceremony。
