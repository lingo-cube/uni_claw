# Phase 2 — LOCAL_UNICLAW Debug Extension 记录

> 前置：`FLOW_V2_CONFORMANCE_PASS`（见 2026-09-06-phase1-flow-conformance.md）。

## 执行记录

- **Gate 1 → EXPLORE_RESOLVED**：Intent（directive 已裁决 extension 架构）；
  迁移源经 `git show uni-agent:.ai/skills/{evidence-driven-debugging,
  runtime-behavior-debugging}/SKILL.md` 逐节取证（302+116 行，E0-E4 双表、
  A-F 分类学、Reality 模板、Ownership、3 案例、STOP 全文提取）。
- **Gate 2 → EXECUTION_READY**：Direct（边界裁决依赖 Leader 热上下文：
  哪些语义属 extension、哪些归上游/uniflow）。
- **Implementation**：`.agents/skills/uniclaw-debug-evidence/SKILL.md` +
  `references/canonical-cases.md`；frontmatter `source: LOCAL_UNICLAW`。
- **发现机制验证**：skill 创建后 DSH catalog 实时出现
  `uniclaw-debug-evidence`（与 uniflow 同路径同机制）。
- **Review**：Built Right——composition contract 显式排除 fix/TDD/review/
  verify/complete 所有权；通用循环零复制（仅指回 diagnosing-bugs）✓。
  Built Right Thing——11 项标准逐条对应（见下）✓ → APPROVE。
- **Gate 3 → VERIFIED / Gate 4 → COMPLETE**。

## Exit Criteria（11/11）

| # | 判据 | 结果 | 证据 |
|---|---|---|---|
| 1 | E0-E4 仍有效语义找到真实来源 | PASS | `git show uni-agent:.ai/skills/evidence-driven-debugging/SKILL.md` L150-158 + runtime-behavior L56-69（逐字迁移） |
| 2 | FDP 语义迁移无丢失 | PASS | extension §3 模板（Expected/Observed/Gap/FDP + 最短人类路径 falsifiable 假设） |
| 3 | Owner localization 迁移 | PASS | extension §4（owner + 不触碰声明） |
| 4 | Runtime evidence semantics 迁移 | PASS | extension §1 双表 + E0-E1/E2-E4 规则 |
| 5 | failure taxonomy 有 buyer 部分迁移 | PASS | extension §2（A-F + lifecycle/last-correct/invariant） |
| 6 | generic debugging 未本地重复实现 | PASS | extension 无 reproduce/hypothesis/fix 循环；仅 composition 指回上游 |
| 7 | LOCAL_UNICLAW 不拥有 fix/TDD/review/verify/complete | PASS | frontmatter description + §0 显式排除 |
| 8 | diagnosing-bugs 是唯一 generic Debug entry | PASS | 本分支无其他 debug 入口（catalog 可核） |
| 9 | 只能作为 diagnostic extension 被组合 | PASS | §0 composition contract 单向（被调用→返回证据） |
| 10 | uni-harness core 不含 Product model | PASS | 产品 seam 仅以名字出现在 LOCAL_UNICLAW extension 内；uniflow/schema/AGENTS 零改动（除 required_skills enum 增补本 skill 名） |
| 11 | 旧 Skill 无未迁移 unique semantics | PASS | 处置映射表（provenance inventory 新增节）逐节覆盖两旧 skill 全部章节 |

## 声明

```text
LOCAL_UNICLAW_DEBUG_EXTENSION_READY
```

（结构完成；真实行为验证属 Phase 3。）
