# ING-006 — Observation Ingress Semantic Kind & Proposal Rename · DETERMINISTIC Evidence

> changes/ING-006/state.md · 2026-09-09 · base 221e9444 · 工作树 diff
> 复现命令：`dotnet test UniClaw.Kernel.slnx` + `grep -rn ObservationRecord src tests --include=*.cs`

## 四元组

```yaml
level: DETERMINISTIC
method: >
  TDD：RED（机械迁移批：rename/kind+context 字段/EvidenceId 派生/E2B 冻结面
  机械替换，行为零变更——既有 56 用例全保持；新用例 N1-N6 先行，5 红
  [N1/N2/N3/N4/N6] 1 绿 [N5 机械项]）→ GREEN（三项行为：kind/context
  recognized fail-closed、relevance kind 分支、MaterialEffect kind∧context
  判定门）→ REVIEW（fresh SubAgent 六轴 APPROVE；F1/F2/F3 全修：陈旧注释
  ×2、context 对称校验 + N7）→ 复跑 + grep 残留检查 + 台账同步
expected: >
  验收 8 条全 GREEN；全量 63/63（Kernel 46 = 既有 39 + N1-N7；Agent 17
  零改动）；grep ObservationRecord 仅余 1 处有意"前名"历史注记；E2B 8
  条验收断言零改动（D1 语义冻结首次执行）
actual: >
  RED：失败 5 / 通过 57。GREEN：失败 0 / 通过 62。Review 修复（F2
  context-recognized + N7）后：失败 0 / 通过 63（Kernel 46 + Agent 17）。
  grep 残留 = 1（ObservationProposal.cs:34 有意历史注记）。E2B 断言改动
  数 0；C2E 断言改动数 0；OUT/GEV 仅 helper 机械参数。改动面 = plan
  After 表 + ObservationIngressTests.cs（新）零意外；红线零触碰（无
  anti-spoofing 能力声称——Review A2 轴专项核证）
evidence: 本文件
```

## 验收 ↔ 证明映射

| # | 验收 | 证明 |
|---|---|---|
| 1 | rename 零残留 | grep 双证 + 编译；git RM 记录 |
| 2 | kind 落地 + fail-closed | N1（kind-recognized）+ N7（context-recognized 对称，Review F2）；EvidenceRecord 携带两字段 |
| 3 | AttemptReport kind 门 | ExportAttemptEvidence（Kind=AttemptReport+Context 显式）；N4（subject 在 scope 内仍非 world-relevant）；C2E Accepted5 迁移 GREEN |
| 4 | MaterialEffect policy 迁移 | N3 正例（capability producer + PostActionEffectFlow → 满足；误拒消除）；OUT 既有 MaterialEffect 用例迁移 GREEN |
| 5a | kind 门压 context 门 | N2（AttemptReport + PostActionEffectFlow → 不满足） |
| 5b | 能力声称边界 | Review A2 轴 PASS（全仓 grep 无 spoofing 声称；Deferred ⑦ 如实注记于 Ledger/Assurance/测试头）；无运行时用例（不可测试性即内容） |
| 6 | E2B 语义冻结 | 断言改动数 0（Review A3 逐行核对）；8 用例 GREEN |
| 7 | C2E/OUT/GEV 回归 | 断言零变化，helper 机械参数；10/18/17 全 GREEN |
| 8 | 台账同步 | VERIFY 步：P2/P3 known gap 消除 + 冲突台账闭合 + CONTEXT 三处（Observation Context 词条 / Evidence Record 字段 / Obligation Fulfillment policy 措辞） |

## Review 记录

- 六轴 fresh SubAgent：**APPROVE**——A1 kind/context 门次序（N2/N3/N6 三分支齐备）/ A2 ⑤b 能力声称边界（4 处 grep 全为如实边界陈述）/ A3 E2B 语义冻结（Assert 行改动 0）/ A4 rename 完整性 / A5 决策对齐（D1-D8 逐条）/ A6 意外改动（12 文件逐 hunk）全 PASS
- F1（minor）EvaluateObligations 陈旧 producer-prefix 注释 → 已改为 kind∧context 措辞
- F2（minor）context 无对称 Enum.IsDefined → 已补 `context-recognized` + N7
- F3（nit）"防自述冒充"孤立易误读 → 已改"原以 producer 前缀粗略收窄的语义职责改由 context 字段承担"

## 台账核对（验收 8）

新增 N1-N7 七用例全落盘；迁移面：E2B helper（类型名 + 默认参数）/ C2E helper + 3 post-action 调用点 / OUT helper + ObserveEffect + 迟到 attempt reflux（显式 kind）/ GEV helper + 1 调用点；删除 ProducerMatches 前缀判定路径；E2B 8 / C2E 13 / OUT 18 / GEV 17 断言零变化。总数 56 → 63。
