# ARCH-DOC-015 — docs 五层分类学 + 规则冻结 + 确定性执法
lifecycle_state: closed · disposition: implemented · depth: standard · base: 62054a26

## Intent（WHAT/WHY）
docs/analysis/ 16 份文档混装三类异质物（分析/方案/已定架构），且旧规则
（UWM-009 两条）无词汇表、无体例、零执行——已建成组件架构被迫自贬候选
（perception-provider 文档）、体例漂移无人拦（per-005 两份）。本 change
重写放置规则为五层分类学并 FROZEN，配确定性执法测试，然后按新规则完成
补债与迁移。

## Scope
- `docs/README.md` 重写并 FROZEN：五层分类学（prd/design/analysis/
  architecture/adr）+ 状态词汇表 + 分行头部体例 + 升格路径（含 §3.3
  已建成组件架构可直接冻结为 COMPONENT-BASELINE）+ 执法声明 + 索引要求。
- `tests/UniClaw.Kernel.Tests/DocsMetadataTests.cs`（4 用例）：analysis/
  design 头部与禁词（\bFROZEN\b/\bCLOSED\b 词边界，不误伤 R0_CLOSED 复合
  词）、architecture 冻结声明、prd LOCKED/ACCEPTED、adr 文件名模式；
  README.md 豁免。
- 补债：per-005 两份头部改分行标准体例（内容零改动）。
- 迁移：方案三份 → design/（git mv）；perception-provider-architecture
  → `docs/architecture/perception-provider-baseline-v0.1.md` 并升格
  FROZEN / COMPONENT-BASELINE（§8 改为修订规则）。
- 索引：analysis/design/prd 三份 README.md。
- 本 state。UAR-001 未跟踪文档零触碰（其落地时按新规则归 design/）。

## Out of Scope（禁止）
- 不动冻结产品基线/协议正文；不动 ADR 正文；不造空 PRD（首个真实 buyer
  出现再入档）；harness-v2 其余四份判为分析留守（用户裁量点已确认）。

## Decisions
- D1 PRD 生命周期：草案驻 design/（DRAFT）→ 接受迁 prd/（LOCKED）——
  每目录单一状态语义，与 analysis→architecture 升格同构。
- D2 执法主体 = C# 测试（进全量回归，零新依赖零人工），优于独立脚本
  （validate-model-bindings 先例弱：手动跑）。
- D3 已建成组件架构（落地代码 + ADR 支撑）直接冻结 COMPONENT-BASELINE
  ——修复「既成权威被候选目录抹掉」的分类学缺陷。

## Acceptance / Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >-
    RED 证明（执法测试先跑出旧债：per-005×2 缺标准头 + perception 合并式
    Authority 头）→ 补债/迁移 → DocsMetadataTests 4/4 GREEN → 全量回归。
  expected: 分类学违规零残留；全量零破坏
  actual: >-
    RED 精确暴露三份债后修复；DocsMetadataTests 4/4 GREEN；全量
    Kernel 351/351（+4 执法测试）+ Agent 17/17。
  evidence: 本 state（RED→GREEN 序列即证明）
```

## Status log
2026-09-12 · enter→understanding · Human 诊断确认：分类学缺陷（锁定物驻
  analysis / 方案与分析混装）+ 规则不完整 + 零执行；三问定案（PRD 层 /
  先锁规则 / C# 测试执法）。
2026-09-12 · understanding→resolved→persisted→implementing · 规则重写
  FROZEN → 执法测试（首跑 RED 暴露三债）→ 补债 + 迁移（design/ 三份、
  组件基线升格）→ 三索引。
2026-09-12 · implementing→closed · DocsMetadataTests 4/4 + 全量 351/17
  GREEN；规则已被证明在执法（RED→GREEN 全程留痕）。
