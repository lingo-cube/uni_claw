# SKL-002 — Vendor stop-that-shit skill（实现期反过度设计）

lifecycle_state: closed · disposition: none · depth: standard · base: 40f190c6

## Intent（WHAT/WHY）

采纳 `lennney/stop-that-shit`（v0.2.2，上游 7f3dc86）主 skill：实现期范围
纪律的 operational HOW（Stop Ladder：明确责任 → 直接方案 → 仅为具体缺口
扩展 → 按实际效果评判防御 → 用项目检查验证后收尾）。

填补空白：通用基线「简化优先」是原则（WHAT），REVIEW 在事后抓越界，
但 IMPLEMENT 当下「做到哪算够」的判断方法此前无 skill 覆盖。附带收益：
VERIFY 循环的收尾判据（证据仍有效即复用；无关绿色命令替代不了缺失证据）、
blocker 如实上报（对齐 BLOCKED disposition 与 A9 CLOSED 判定）。

## Scope / Out of Scope

Scope：`npx skills add lennney/stop-that-shit -s stop-that-shit -a codex -y
--copy` 标准安装（上游原文不改）；skills-lock.json 留痕；本 state.md。

Out of Scope：stss（决策文档去废话 skill，继续搁置，随时可同路径安装）；
Guard hooks 任何形式（SKL-001 Decisions #3 不变）；UniFlow / model-routing /
schemas 改动；上游 fork。

## Decisions

1. **影响评估结论：优化且轻量，不造成遗漏**（2026-09-16 评估，本会话）：
   - 结构：零新增流程步骤 / 门槛 / 仪式，Part A 冻结不动；
   - 成本：仓库纯新增 3 处；~167 行按需加载，与 tdd 同量级，不触发即零成本；
   - 覆盖：不删不改既有机制；定位是安全网中间层（事前 Change State →
     事中 stop-that-shit → 事后 REVIEW），三段互不替代。
2. **两个观察点**（装后前几次真实使用回看一次）：
   a. tdd 摩擦：偷懒读法可能把边界测试当「投机防御」跳过。skill 原文已
      约束（"Honor explicit acceptance criteria and mandatory checks"）；
      tdd 是本仓显式默认，拿它当借口跳过 RED 测试 = REVIEW 缺陷。
   b. Kernel 防御代码误伤：UniClaw.Kernel 是 assurance 密集产品
      （RuntimeAssurance / PostActionEffectVerification / SealedTraceStore），
      Worker 可能删掉自己没看懂的保护。skill 自带护栏（"Missing evidence
      is not proof that it is unnecessary"），产品测试 + REVIEW 兜底。
      ——唯一有真牙的风险。
3. **惰性指令文本接受**：SKILL.md 携带 Codex/Claude 插件指令语法
   （`$stop-that-shit lock` / `agents=N`），在 DSH 与 vendor 形态 Codex 下
   均为死文本不执行；按「上游原文不改」保留，小概率被模仿输出属无害可观察。
4. 上游触发边界经成对评测（scope / proof-stop / dependency / hash /
   compatibility，好坏夹具），trigger 纪律是测过的。

## Acceptance

1. `skills-lock.json` 含 stop-that-shit 条目：source=lennney/stop-that-shit，
   skillPath=skills/stop-that-shit/SKILL.md，
   computedHash=87c68bca65d8819bb739038591c5e43abf07b756f227f708dce2b808164c87bc。
2. `.agents/skills/stop-that-shit/` 恰含 SKILL.md + agents/openai.yaml，
   无 hooks/ 带入。
3. DSH 会话 skill 目录出现 stop-that-shit（消费侧生效）。
4. 本次仓库 delta 仅：skills-lock.json（+stop-that-shit 一条）、
   .agents/skills/stop-that-shit/、本文件。

## Constraints

- 第三方 skill 上游原文不改（AGENTS.md §3）。
- Skill 只回答 HOW；不进 UniFlow 路由表、不拥有生命周期（AGENTS.md §2/§3）。
- Guard 不装（SKL-001 Decisions #3 的触发条件与升级路径继续有效）。
- 环境注意：安装/更新需 `export npm_config_cache=<可写目录>`（~/.npm 不可写）。

## Verification

```yaml
verification:
  level: CONTRACT
  method:    "find .agents/skills/stop-that-shit -type f && python3 -c \"import json;print(json.load(open('skills-lock.json'))['skills']['stop-that-shit'])\" && git status --short skills-lock.json .agents/skills/"
  expected:  skill 目录恰 2 文件、无 hooks；lock 含指定 hash 条目；delta 仅 lock + skill 目录 + 本文件
  actual:    全部符合（2026-09-16 命令输出）；DSH 目录刷新含 stop-that-shit 条目
  evidence:  本会话命令输出；目录刷新通知
```

## Status log

- 2026-09-16 · understanding→closed · 单会话完成：影响评估（优化/轻量/
  遗漏三问，用户质询后补充）→ 用户批准 skill-only → 安装 → CONTRACT 验证通过。
