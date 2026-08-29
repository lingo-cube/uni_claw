# Project Skills — 注册与发现机制

> 定位: Skill 本体定义、通用发现机制、Host adapter 与调用边界（跨 AI Coder 共用）。
> 上级: AGENTS.md「Where Is Truth」。Skill 只提供执行方法，**不产生 Architecture Authority**，
> 也不自动授权文件修改、Lifecycle 变更或 Architecture Decision。

## 唯一本体

- `.ai/skills/<name>/SKILL.md` 是项目 Skill 正文唯一来源。
- `.agents/skills/` 与 `.dsh/skills/` 只允许保存指向 `.ai/skills/` 的项目内相对符号链接。
- Host adapter、历史 OpenSpec 与用户全局 Skill 目录都不是项目 Skill 真相源。

## 通用 AI Coder 如何发现

- 采用 `.agents/skills` 约定的 Host 原生扫描项目各级 `.agents/skills`，不要求直接扫描 `.ai/skills`。
- 需要 Host 原生发现时，只允许在
  `.agents/skills/<name>` 创建指向 `../../.ai/skills/<name>` 的**项目内相对符号链接**。
- `.agents/skills` 是通用发现 adapter，不是第二份 Skill 真相源；符号链接必须由
  `scripts/check-consistency.sh` 守护，禁止绝对路径、悬空链接或普通目录副本。
- 仅修改 Skill 内容时，adapter 会直接反映变更；新增或改名 Skill 时必须同步
  adapter 与一致性检查。

## DSH 如何发现

- DSH skill-filesystem 只扫固定根：`<project>/.dsh/skills`、`<project>/.agents/skills`、
  `customSkillDirs`、`~/.dsh/skills`、`~/.agents/skills`；不直接扫描 `.ai/skills`。
- 本项目优先让 DSH 复用 `.agents/skills`；为兼容只读取 DSH 固定根的 Host，
  `<project>/.dsh/skills` 仅保留指向 `.ai/skills` 的**相对符号链接**，
  DSH 以 rank 100 自动发现，无需 dsh host 配置改动。
- 换机/克隆后 `.dsh/skills` 符号链接随仓库还原即生效；若某环境不还原
  （`core.symlinks=false` 等），跑 `scripts/setup-dsh-skills.sh` 幂等重建
  （会跳过悬空/指向项目外的源）。
- 新增/改名 Skill：保证该 Skill 在 `.ai/skills/` 下有带
  `name` + `description` frontmatter 的 `SKILL.md`；如需 DSH 可见，重跑
  `scripts/setup-dsh-skills.sh` 更新 `.dsh/skills`。
- 仅修改 Skill 内容时，现有符号链接直接反映变更，不需要重跑 setup。

## 调用规则

- 仅当用户明确点名某个 skill，或任务清晰匹配其 `SKILL.md` frontmatter 的
  `name` / `description` 时，才在 `PROJECT_CONTEXT_RESOLUTION` 填写 `Required Skill`。
- 选定 skill 后，必须在执行前**完整读取**对应 `SKILL.md`。
- 不默认加载全部 skill；没有匹配项时填写 `Required Skill: NONE`。
- 多个 skill 同时适用时，只选择覆盖任务所需的最小集合，并声明使用顺序。

## WorkItem 显式传递

- UniFlow Leader 必须把选中的 Skill 名称按执行顺序写入 `required_skills`；不得只在
  对话或 `semantic_brief` 中暗示 Worker 应该使用某个 Skill。
- Bug / 失败调查至少选择 `evidence-driven-debugging`；Runtime、FSM、Traversal、
  Recovery、Async、真机、flaky 或 nondeterministic 问题再追加
  `runtime-behavior-debugging`。
- Worker 通过 `tools/agent_profile_validator.py context` 获取 canonical
  `context_sources.required_skills`，并在动作前完整读取；解析失败时 fail-closed。
- `required_skills` 只传递执行方法，不产生权威、不扩大 scope，也不触发 Worker fanout。

## 禁止

- 复制全部 Skill 内容到 `AGENTS.md`
- 默认预加载全部 Skill
- 修改 Architecture v1
- 创建新的 Decision 或 Gate
- 在 `.agents/skills` 或 `.codex/skills` 复制、维护 Skill 本体
- 创建指向项目外、使用绝对路径、已经悬空或未受一致性检查保护的 Skill adapter
- 修改 Runtime、Test、OpenSpec（未经所属 gate）
- 修改现有符号链接，除非只读验证确认链接已经失效
