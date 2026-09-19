# SKL-001 — Vendor lucid skill（plain-language 沟通塑形）

lifecycle_state: closed · disposition: none · depth: standard · base: 40f190c6

## Intent（WHAT/WHY）

采纳 `ustas-eth/lucid`（v0.2.1，上游 4778d5a）为本仓 skill：被调用时以平实、
熟悉的语言作答/重写，保留技术含义与细节（ISO 24495-1 plain-language 原则）。
填补空白：现有 17 个 skill 无一覆盖「表达可懂度」——show-me 管视觉化、
grilling 管挑战用户、code-review 管事后审查；用户↔agent 报告通道的清晰度
此前没有任何 HOW 支撑，仅有通用基线「有歧义先说明」的 WHAT 要求。

关键事实修正（评估中被证伪的反对理由）：ISO 24495-1 官方摘要明确
"applies to most, if not all, written languages, but it provides examples
only in English"——标准语言无关，可平移中文（治 LLM 中文输出的翻译腔/
黑话堆砌）；CEFR B1 / ASD-STE100 仅为英文辅助参考。

## Scope / Out of Scope

Scope：`npx skills add ustas-eth/lucid -s lucid -a codex -y --copy` 标准安装
（上游原文不改）；`skills-lock.json` 留痕；本 state.md。

Out of Scope：stop-that-shit / stss 安装（见 Decisions）；Guard hooks 任何
形式；UniFlow / model-routing / schemas 任何改动；lucid 内容本地化或 fork。

## Decisions

1. **lucid-only 采纳**。三候选（lucid / stop-that-shit / stss）评估中唯一
   本次通过：沟通塑形、正交于流程、零生命周期冲突、零常驻成本；上游 eval
   以 literal-preservation / 技术区分保留 / already-clear-不改动 三类
   smoke 用例验证了最大风险（简化摧毁技术精度）。
2. **stop-that-shit 与 stss 暂缓**（用户决定，非否决）。评估结论：主 skill
   填补实现期反过度设计空白（Stop Ladder 为 operational HOW，与通用基线
   「简化优先」互补不重复），skill-only 形态合规，价值高；stss 服务决策
   文档行文，价值中。两者随时可按同路径安装，无需重新评估。
   评估材料：上游克隆（临时，/tmp/skill-eval，已失效可重取）。
3. **Guard 明确不装**。stop-that-shit 的机器强制层（文件锁 / agents=N /
   mode 系统）与 Change State Out-of-Scope、WorkItem forbidden /
   frozen_decisions、UniFlow B2 委派判据构成平行权威；且 5 个 host adapter
   （Codex/Claude/OpenCode/Hermes/Pi）无 DSH，违反 B5 双 host 对称。
   触发条件（REVIEW 漏放越界改动 ≥2 次，A8 标准）成立时：先走 UniFlow
   原生解法（确定性脚本：git diff vs Out-of-Scope / forbidden），不足再
   评估「收敛式集成」（Guard 作为 WorkItem 契约的 adapter 执行器，需先立 ADR）。
4. **安装形态**：skill-only vendor（--copy，24 行上游原文不改）；lucid 不进
   UniFlow A3 路由表（它不是不确定性解析）；`$lucid` 指令语法是 Codex 形式，
   DSH 下以自然语言或 description 触发等效。

## Acceptance

1. `skills-lock.json` 含 lucid 条目：source=ustas-eth/lucid，
   skillPath=plugins/lucid/skills/lucid/SKILL.md，
   computedHash=3262de45d2ef17319bb57842e3f516e1220c9be5db6dee80179c2b7dfe266052。
2. `.agents/skills/lucid/` 恰含 SKILL.md + agents/openai.yaml，无 hooks/ 带入。
3. DSH 会话 skill 目录出现 lucid（消费侧生效）。
4. 本次仓库 delta 仅：skills-lock.json（+lucid 一条）、.agents/skills/lucid/、
   本文件。

## Constraints

- 第三方 skill 上游原文不改（AGENTS.md §3）。
- Skill 只回答 HOW，不拥有生命周期、不进流程主干（AGENTS.md §2/§3）。
- 环境注意：本机 ~/.npm 在 DSH 沙箱下不可写，安装/更新需
  `export npm_config_cache=<可写目录>`。

## Verification

```yaml
verification:
  level: CONTRACT
  method:    "python3 -c \"import json;print(json.load(open('skills-lock.json'))['skills']['lucid'])\" && find .agents/skills/lucid -type f && git diff --stat skills-lock.json"
  expected:  lock 含指定 hash 条目；skill 目录恰 2 文件；lock diff 仅 +lucid
  actual:    全部符合；hash 与安装前 scratch 目录对拍一致（/tmp/lucid-install-test）
  evidence:  本会话命令输出（2026-09-16）；DSH 目录刷新含 lucid 条目（Acceptance #3）
```

## Status log

- 2026-09-16 · understanding→closed · 单会话完成：三 skill 评估（含 Guard
  深析与 ISO 官方摘要核验）→ 用户批准 lucid-only → 安装 → CONTRACT 验证通过。
