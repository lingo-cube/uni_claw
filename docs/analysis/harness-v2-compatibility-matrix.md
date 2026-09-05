# Harness V2 — Compatibility Matrix（双 Harness 能力对照）

> DocumentType: `HARNESS_MIGRATION_ANALYSIS`
> Status: `COMPLETE / EVIDENCE-BACKED`
> Authority: `NONE`
> 调查基线: `uni-agent` 谱系 + 上游实测（mattpocock/skills、humanlayer/skills、
> vercel-labs/skills CLI、openai/agents.md）。
> 本版刷新: 2026-09-06（WorkItem `workitems/WI-P1-B-001.json`），对齐多次重定位后的
> repo 现状：uniflow 已迁为 `.agents/skills/uniflow/SKILL.md`（ADR-0005）、`.dsh/` 与
> `decisions/` 已删除（由 `docs/adr/` 取代）、四 Semantic Gate 已冻结（ADR-0004）。
> 禁止边界: 本文件是调查记录，不构成协议变更授权。

## 0. 上游地面真相（实测）

| 项目 | 事实 | 证据 |
|---|---|---|
| AGENTS.md 规范 | OpenAI 开放格式「README for agents」：repo 级持久指令、支持嵌套 | github.com/openai/agents.md README |
| Skill 格式 | `<skill>/SKILL.md`（frontmatter: name/description，可选 `disable-model-invocation`）+ 同目录 references | `.agents/skills/*/SKILL.md` 文件实测 |
| 安装器 | `skills` CLI：`add/remove/list/update`，项目级落点 **`.agents/skills/`**，`--copy` 复制 / 默认符号链接，`skills-lock.json` 记录 provenance | 本 repo `skills-lock.json`（9 个上游条目）+ CLI 实际执行 |
| Matt 上游 | github.com/mattpocock/skills（8 个 skill 被 vendored：engineering/productivity 目录） | `skills-lock.json` 各条目 `source: mattpocock/skills` + `skillPath` |
| show-me 上游 | github.com/humanlayer/skills，`plugins/show-me/skills/show-me/SKILL.md` | `skills-lock.json` show-me 条目 |
| Codex 消费 | `npx skills@latest ls` 列出全部 10 个 skill（含 `uniflow`，Source: local），每条均报 Agents: **Codex, GitHub Copilot**（`.agents/skills/` 注册） | `npx -y skills@latest ls` 输出（2026-09-06 复跑） |
| DSH 消费 | 安装后 DSH 会话 skill catalog 实时出现新 skill → **DSH 直接发现 `.agents/skills/`** | §0 原始 leader-attested 实测行，本文件沿引（引证性质：DSH 会话 catalog 为会话内事实，本次 Worker 未自行观测，只核对文件层） |

### 0.1 重定位核对（本次刷新的文件级验证）

| 主张 | 结果 | 验证 |
|---|---|---|
| uniflow 是 LOCAL_UNIFLOW skill，驻标准位置 | ✅ frontmatter `source: LOCAL_UNIFLOW`，SKILL.md 224 行 | `.agents/skills/uniflow/SKILL.md` 头 6 行 + ADR-0005 |
| `.agents/skills/` 共 10 个目录（9 上游 + uniflow） | ✅ | `ls .agents/skills/` |
| `skills-lock.json` 仅含 9 个上游条目（本地 skill 不入 lock） | ✅ | `skills-lock.json` |
| `.dsh/` 已移除 | ✅ `ls .dsh` → No such file or directory | 文件系统 |
| `decisions/` 已由 `docs/adr/` 取代 | ✅ `decisions/` 不存在；`docs/adr/` 有 5 个 ADR（0001–0005，均 accepted） | `ls docs/adr/` |
| 四 Semantic Gate 已冻结 | ✅ Explore Resolution / Execution Readiness / Verification / Completion，状态 accepted，2026-09-06 | `docs/adr/0004-four-semantic-gates.md` |
| WorkItem 为 delegation contract | ✅ ADR-0002；本任务自身即以 `workitems/WI-P1-B-001.json` 派发 | `docs/adr/0002-workitem-is-dispatch-protocol.md`、`ls workitems/` |
| `plans/`、`evidence/` 存在且有内容 | ✅（plans/evidence 各含 README 及记录） | `ls plans evidence` |

## 1. 能力矩阵

| Capability | Generic Expectation | Codex | DSH | Gap |
|---|---|---|---|---|
| Repo instruction | scoped persistent instruction（AGENTS.md 开放格式） | ✅ 原生读 AGENTS.md；installer 注册 Codex agent（证据: `npx skills ls` Agents 行） | ✅ 读入 AGENTS.md 为 workspace instructions（§0 leader-attested；本会话注入其 workspace instructions 亦为旁证，引证性质同 §0） | 无结构性 gap；DSH 侧无嵌套 AGENTS.md 逐级合并证据（未测，低风险） |
| SKILL.md | reusable portable procedure | ✅ `.agents/skills/<name>/SKILL.md`，10/10 被 `npx skills ls` 列出（证据: 该命令 2026-09-06 输出） | ✅ 实时发现同目录（§0 leader-attested catalog 行） | `grill-with-docs` 与 `setup-matt-pocock-skills` 均带 `disable-model-invocation: true`（证据: 两者 SKILL.md frontmatter），不入模型 catalog 属 by design；上游可见性「待查」项已闭合（见 provenance inventory） |
| uniflow 控制面（LOCAL_UNIFLOW） | 流程真相源单点、双侧同源发现 | ✅ `npx skills ls` 列出 uniflow（Source: local, Agents: Codex, GitHub Copilot）；Codex 按需加载 | ✅ DSH 会话 catalog 出现 uniflow skill（证据: §0 leader-attested 行 + `docs/analysis/harness-v2-skill-provenance-inventory.md` 表末行「catalog 实测即时发现」） | 无 adapter；`skills-lock.json` 不含本地 skill（by design，provenance 以 frontmatter `source: LOCAL_UNIFLOW` 声明） |
| Skill discovery | on-demand | ✅ skills CLI 管理 + Codex 按需加载（证据: `npx skills ls`） | ✅ catalog 动态更新（§0 leader-attested） | 无 |
| WorkItem | portable task intent（schema 禁 harness 专有字段；仅 delegation contract，ADR-0002） | ✅ `schemas/work-item.schema.json` harness-neutral | ✅ 同一 schema；DSH 侧已有 payload 落地先例（`workitems/WI-P1-B-001.json`，即本任务派发载荷——文件层事实；会话内 dispatch 机制属 adapter） | DSH 侧 WorkItem dispatch 的产品化入口未单建（沿用 adapter 职责，非共享层缺口） |
| Fresh context | disposable execution | ✅ Codex threads 语义兼容 | ✅ DSH session/run 模型兼容（本任务即以独立 subagent fresh context 执行——旁证；协议层见 `.agents/skills/uniflow/SKILL.md` §委派协议） | 两侧均未做「同 WorkItem 双执行」一致性测试（仍开放） |
| Model routing | 与任务语义分离 | ✅ model-routing.yaml 只到 tier；Codex 绑定留 adapter（证据: `model-routing.yaml` + AGENTS.md §2 真相表） | ✅ 同一映射；DSH 绑定留 adapter | adapter 绑定配置两侧均未建（仍开放） |
| Tool execution | harness adapter concern | ✅ Codex tools 在 adapter 层 | ✅ DSH tools 在 adapter 层 | 无共享层污染：`.agents/skills/`、`schemas/` 内无 host 专有块（证据: 目录清单 + AGENTS.md §4 禁止事项） |
| Evidence return | normalized completion evidence | ⚠️ `evidence/` 约定 + verification 记录；无机械 gate（证据: `ls evidence/`） | ⚠️ 同左；本任务以 STATUS/CHANGES/EVIDENCE/UNRESOLVED 结构返回即现行约定的实例 | 统一规范化六元组尚未落 schema（仍开放；Completion Gate 语义已冻结于 ADR-0004） |

## 2. 结论

1. **Skill 层零 gap**：installer 标准输出 `.agents/skills/`（现 10 目录）同时被 Codex
   （`npx skills ls` 注册）与 DSH（leader-attested catalog 实测）消费——单一 canonical
   源成立，无需任何 adapter。
2. **Instruction 层零 gap**：AGENTS.md 开放格式即两侧共同入口。
3. **uniflow 单点真相成立**：控制面以 LOCAL_UNIFLOW skill 驻标准位置（ADR-0005），
   Codex（`skills ls`）与 DSH（catalog，leader-attested）同源发现；AGENTS.md §2 仅持
   指针。
4. **真实 Gap 只剩三处**：DSH WorkItem dispatch 产品化入口、双侧 model 绑定 adapter
   配置、cross-harness 一致性测试与 Result 规范化 schema。
5. **V1 遗留不对称已消除且未回潮**：`.ai/skills` 三副本与 `.dsh/` 均已不存在
   （文件系统验证）；`.agents/skills/` + `skills-lock.json` 是唯一真相。

## 3. 观察记录（不改变结论）

- `skills ls` 显示的 agent 注册（Codex, GitHub Copilot）来自 installer 生态约定；
  本地 `uniflow` 同样获得注册（Source: local）。
- `grill-with-docs` 此前「DSH catalog 可见性待查」：核对其 SKILL.md frontmatter 含
  `disable-model-invocation: true`，与 `setup-matt-pocock-skills` 同类——不进模型
  catalog 是上游设计（用户显式调用型），provenance inventory 的待办已闭合。
- 首次安装尝试失败于 `-s` 逗号串（CLI 视为单名）；正确用法是重复 `-s` 旗标——
  已记入 AGENTS.md 安装命令示例。
- 历史路径（`.dsh/`、`decisions/`、根 `uniflow.md`、`skills/` 自撰版）均已删除，
  本版矩阵不再引用；provenance 见
  `docs/analysis/harness-v2-skill-provenance-inventory.md`。
