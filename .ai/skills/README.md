# Project Skills — 注册与发现机制

> 定位: skill 本体定义、DSH 发现机制、调用规则与禁止事项（跨助手共用，Codex + Claude）。
> 上级: AGENTS.md「Where Is Truth」。Skill 只提供执行方法，**不产生 Architecture Authority**，
> 也不自动授权文件修改、Lifecycle 变更或 Architecture Decision。

## 本体在哪

- `.ai/skills/` — 跨助手协议（Codex + Claude 共用）
- `.claude/skills/` — OpenSpec playbook / 推导类

## DSH 如何发现

- DSH skill-filesystem 只扫固定根：`<project>/.dsh/skills`、`<project>/.agents/skills`、
  `customSkillDirs`、`~/.dsh/skills`、`~/.agents/skills`；**不扫 `.claude/skills` / `.ai/skills`**。
- 本项目用 `<project>/.dsh/skills` 下的**相对符号链接**（指回 `.ai/skills` / `.claude/skills`）接入，
  DSH 以 rank 100 自动发现，无需 dsh host 配置改动。
- 换机/克隆后 `.dsh/skills` 符号链接随仓库还原即生效；若某环境不还原
  （`core.symlinks=false` 等），跑 `scripts/setup-dsh-skills.sh` 幂等重建
  （会跳过悬空/指向项目外的源）。
- 新增/改名 skill：保证该 skill 在 `.claude/skills/` 或 `.ai/skills/` 下有带
  `name` + `description` frontmatter 的 `SKILL.md`；如需 DSH 可见，重跑
  `scripts/setup-dsh-skills.sh` 更新 `.dsh/skills`。
- 仅修改 Skill 内容时，现有符号链接直接反映变更，不需要重跑 setup。

## 调用规则

- 仅当用户明确点名某个 skill，或任务清晰匹配其 `SKILL.md` frontmatter 的
  `name` / `description` 时，才在 `PROJECT_CONTEXT_RESOLUTION` 填写 `Required Skill`。
- 选定 skill 后，必须在执行前**完整读取**对应 `SKILL.md`。
- 不默认加载全部 skill；没有匹配项时填写 `Required Skill: NONE`。
- 多个 skill 同时适用时，只选择覆盖任务所需的最小集合，并声明使用顺序。

## 禁止

- 复制全部 Skill 内容到 `AGENTS.md`
- 默认预加载全部 Skill
- 修改 Architecture v1
- 创建新的 Decision 或 Gate
- 创建 `.agents/skills` 或 `.codex/skills`
- 修改 Runtime、Test、OpenSpec（未经所属 gate）
- 修改现有符号链接，除非只读验证确认链接已经失效
