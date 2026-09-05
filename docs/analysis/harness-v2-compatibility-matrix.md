# Harness V2 — Compatibility Matrix（双 Harness 能力对照）

> DocumentType: `HARNESS_MIGRATION_ANALYSIS`
> Status: `COMPLETE / EVIDENCE-BACKED`
> Authority: `NONE`
> 调查基线: `uni-agent` 谱系 + 2026-09-05 上游实测（mattpocock/skills、humanlayer/skills、
> vercel-labs/skills CLI v1.5.23、openai/agents.md）。
> 禁止边界: 本文件是调查记录，不构成协议变更授权。

## 0. 上游地面真相（实测）

| 项目 | 事实 | 证据 |
|---|---|---|
| AGENTS.md 规范 | OpenAI 开放格式「README for agents」：repo 级持久指令、支持嵌套 | github.com/openai/agents.md README |
| Skill 格式 | `<skill>/SKILL.md`（frontmatter: name/description，可选 `disable-model-invocation`）+ 同目录 references + `agents/openai.yaml`（interface.display_name/short_description） | 上游仓库实际文件 |
| 安装器 | `skills` CLI（vercel-labs/skills）：`add/remove/list/update`，项目级落点 **`.agents/skills/`**，`--copy` 复制 / 默认符号链接，`skills-lock.json` 记录 provenance | CLI --help + 实际执行 |
| Matt 上游 | github.com/mattpocock/skills，`skills/{engineering,productivity,misc,in-progress,deprecated}/<name>/`，37 个 skill | git clone 实测 |
| show-me 上游 | github.com/humanlayer/skills，`plugins/show-me/skills/show-me/SKILL.md` | git clone 实测 |
| Codex 消费 | `skills ls` 报告 Agents: **Codex, GitHub Copilot**（`.agents/skills/` 注册） | 实际执行输出 |
| DSH 消费 | 安装后 DSH 会话 skill catalog 实时出现新 skill（含 show-me）→ **DSH 直接发现 `.agents/skills/`** | 本会话 catalog 变更实测 |

## 1. 能力矩阵

| Capability | Generic Expectation | Codex | DSH | Gap |
|---|---|---|---|---|
| Repo instruction | scoped persistent instruction（AGENTS.md 开放格式） | ✅ 原生读 AGENTS.md；installer 注册 Codex agent | ✅ 读入 AGENTS.md 为 workspace instructions（本会话实测生效） | 无结构性 gap；DSH 侧无嵌套 AGENTS.md 逐级合并证据（未测，低风险） |
| SKILL.md | reusable portable procedure | ✅ `.agents/skills/<name>/SKILL.md`（installer 标准输出） | ✅ 实时发现同目录（catalog 实测） | `grill-with-docs` 未出现在 DSH catalog（待查 frontmatter 可见性）；`setup-matt-pocock-skills` 因 `disable-model-invocation: true` 不入 catalog（by design） |
| Skill discovery | on-demand | ✅ skills CLI 管理 + Codex 按需加载 | ✅ catalog 动态更新 | 无 |
| WorkItem | portable task intent（schema 禁 harness 专有字段） | ✅ `schemas/work-item.schema.json` harness-neutral | ✅ 同一 schema；DSH 侧派发机制属 adapter（未来） | DSH 消费 WorkItem 的 dispatch 入口未建（Phase 8） |
| Fresh context | disposable execution | ✅ Codex threads 语义兼容 | ✅ DSH session/run 模型兼容（V1 profile-adapter 已有 run 级状态先例） | 两侧均未做「同 WorkItem 双执行」一致性测试（Phase 9） |
| Model routing | 与任务语义分离 | ✅ model-routing.yaml 只到 tier；Codex 绑定留 adapter | ✅ 同一映射；DSH 绑定留 adapter | adapter 绑定配置两侧均未建（Phase 7/8） |
| Tool execution | harness adapter concern | ✅ Codex tools 在 adapter 层 | ✅ DSH tools 在 adapter 层 | 无共享层污染（V1 F4 教训已吸收：共享文件无 host 块） |
| Evidence return | normalized completion evidence | ⚠️ `evidence/` 约定 + verification 记录；无机械 gate | ⚠️ 同左；V1 的 ResultGate 思路可在 adapter 重建 | 统一规范化六元组（status/changes/tests/evidence/unresolved/escalation）尚未落 schema（Phase 3 冻结项） |

## 2. 结论

1. **Skill 层零 gap**：installer 标准输出 `.agents/skills/` 同时被 Codex（注册）与
   DSH（实测发现）消费——单一 canonical 源成立，无需任何 adapter。
2. **Instruction 层零 gap**：AGENTS.md 开放格式即两侧共同入口。
3. **真实 Gap 只剩三处**，全部属后续 Phase：DSH WorkItem dispatch 入口、
   双侧 model 绑定 adapter 配置、cross-harness 一致性测试与 Result 规范化 schema。
4. **V1 遗留不对称已消除**：不再有 `.ai/skills` → `.agents`/`.dsh` 手工符号链接
   三副本结构；`.agents/skills/` + `skills-lock.json` 是唯一真相。

## 3. 观察记录（不改变结论）

- `skills ls` 显示的 agent 注册（Codex, GitHub Copilot）来自 installer 生态约定。
- DSH catalog 不显示 `setup-matt-pocock-skills`：上游 frontmatter
  `disable-model-invocation: true` 的预期效果（用户显式调用型 skill）。
- 首次安装尝试失败于 `-s` 逗号串（CLI 视为单名）；正确用法是重复 `-s` 旗标——
  已记入 AGENTS.md 安装命令示例。
