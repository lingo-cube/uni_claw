# DBG-003 — 诊断 packet 标准化出口（schema · 落盘 · 引用）
lifecycle_state: implemented · disposition: none · depth: standard · base: 56a75fd74b5dff72db931edc96e343f0aea6d80b

## Intent（WHAT/WHY）

诊断结论（E-level/class/FDP/Owner…）此前只存在于对话文本与各下游的自造
格式（EVO-001 的内联 `fdp` 段、workbench 的非权威文本），没有可机械消费
的标准形态 —— 诊断因此无法融入主流程。本 Change 在诊断 skill 的权威位置
定义 packet v0：序列化 schema、分级落盘约定、下游引用方式。**不扩大
skill 权力**：packet 依然非权威、不立案、不修复、不宣布完成。

## Scope / Out of Scope

IN：
- `.agents/skills/uniclaw-debug-evidence/references/evidence-packet.md`（新增，
  权威 schema + 落盘分级 + 反作弊 + 引用方式 + cycle-01 真实示例）。
- `.agents/skills/uniclaw-debug-evidence/SKILL.md` §5：追加"序列化与落盘"
  出口契约段（边界语句原样保留）。
- `.agents/skills/uniclaw-evolution-loop/references/case-and-packet.md`：
  case schema 增 `diagnosis` 引用字段 + 冻结规则第 6 条（引用不复制）。

OUT（禁止）：
- 不改 Uniflow SKILL / A3 路由 / state.md 模板（触发器与流程挂点归 EVO-002）。
- 不改 run-diagnosis 工具、RuntimeToolHost、workbench。
- 不新增第二个域诊断命令（等第二次真实使用，ADR-0026）。
- 不把 schema 升入共享层 `schemas/`（等工具侧需要机械校验时再升）。
- 不动 `src/` `tests/` 产品与测试代码。

## Decisions

- D1 packet v0 字段 = SKILL.md §5 七条 + DBG-001 A4 八字段的映射，零新语义。
- D2 分级落盘：E0-E1 且无下游消费 → 对话文本即可；E2+ 或被消费 → 必须落
  `diagnosis.packet.json`（防过度形式化，也防空转）。
- D3 下游只引用（path+sha256），不复制；`authority` 恒 `non-authoritative`。
- D4 EVO-001 cycle-01 的内联 `fdp` 段为过渡形态，后续 cycle 引用 packet v0。

## Acceptance

| ID | method | expected | actual | evidence |
|---|---|---|---|---|
| A1 | 只读核对 reference 字段映射表 | 覆盖 §5 七条 + A4 八字段，无未映射、无新增语义 | PASS | references/evidence-packet.md §2 映射表 |
| A2 | 只读核对 SKILL.md §5 | 出口契约段存在；"不立案/不修复/不宣布完成"边界语句保留 | PASS | SKILL.md §5 |
| A3 | 只读核对 EVO reference | case schema 含 `diagnosis` 字段；冻结规则含"引用而非复制" | PASS | case-and-packet.md §1 |
| A4 | 用 cycle-01 真实数据填示例 | 八字段全部可表达，无需造字段 | PASS | evidence-packet.md §5（E2/B/line18/escalation=SIM-017） |
| A5 | grep 边界检查 | skill 仍含 evidence-only/never owns 语义；无 lifecycle/fix 措辞混入 | PASS | 见 Verification |
| A6 | git status 面 | 仅 2 个 skill 目录 + 本 change；无产品/共享层改动 | PASS | git status 2026-10-23 |

## Constraints

- 三个 skill 文件的修改均为追加/接线，不重写既有段落语义。
- 共享工作区其他会话变更不动。

## Verification

```yaml
verification:
  level: CONTRACT
  method: 文件存在性 + 字段映射核对 + grep 边界（skill 目录内搜 "lifecycle|owns fix|merge" 应无新增权力语义）
  expected: A1–A6 全 PASS
  actual: A1–A6 全 PASS（A5 grep：SKILL.md 中 lifecycle 仅出现于 "lifecycle stage" 观测字段与 §0/§1 边界句，无权力扩张）
  evidence: 本 state + 三个被改文件
```

## Status log

- 2026-10-23 UNDERSTAND→RESOLVE：用户指出"诊断没有很好融入主流程"；定位
  根因 = 诊断产物无标准出口（拉模式、无落盘、无引用）。
- 2026-10-23 PERSIST→PLAN→IMPLEMENT：packet v0 reference + SKILL §5 出口
  契约 + EVO case schema 接线；用户方向"先优化诊断 skill"。
- 2026-10-23 IMPLEMENTED：待 REVIEW（REVIEW 归 code-review 双轴，非本会话
  自评）；后续接线（触发器/Uniflow 挂点/run-diagnosis 出口升级）归 EVO-002。
