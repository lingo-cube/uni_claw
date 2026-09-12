# ARCH-DOC-014 — 感知 Provider Plane 架构文档 + 两条 ADR
lifecycle_state: closed · disposition: implemented · depth: standard · base: e5a7bf70

## Intent（WHAT/WHY）
PER-005/007/008 三 change 落地了一个完整架构层：C# capability 侧（acquisition/
transport/strategy/host）+ Python provider 侧（管道/变体/四层身份/ruleset 治理/
基准）+ 跨进程契约。当前散落在各 change state / evidence / README，无可检索的
架构级叙事——下一个实现者（人或 AI coder）只能逆向工程。本 change 沉淀：
1 份候选架构文档（docs/analysis/，按 docs/README 规则候选态驻留）+ 2 条够格
ADR（双 artifact 锚 / provider 边界与变体选择）。

## Scope
- `docs/analysis/perception-provider-architecture-v0.1.md`（CANDIDATE，
  Authority: NONE）：plane 组成 / 组件 / 契约 / 纪律 / 扩展点 / 与冻结基线
  §8 Capability Plane 的映射与合规核对 / 升级路径。
- `docs/adr/0020-*.md`：双 artifact 确定性锚 = 服务响应 JSON。
- `docs/adr/0021-*.md`：感知 provider 黑盒边界 + 变体「可选择不可携带」。
- 本 state。不改冻结基线、不改产品代码。

## Out of Scope（禁止）
- 冻结基线修改（L0-L3 CLOSED，重开需独立 heavyweight change）；CONTEXT.md
  词汇（PER-006 账上，UAR-001 持有中）；四层身份另立 ADR（平移已评审语义，
  架构文档承载即可）。

## Decisions
- D1 候选驻 analysis/（docs/README 规则 1）；冻结升级 = 后续独立 change。
- D2 ADR 编号 0020/0021（接并发会话未落地的 0019 之后；若冲突以先落地者
  顺延重编号，提交前核对）。
- D3 两 ADR 均满足三判据（难逆转/无上下文会惊讶/真实取舍）。

## Acceptance
- A1 架构文档覆盖：C# 六组件 + provider 内部（pipeline/identity/ruleset/
  bench）+ 契约四层（transport/端点/响应 schema/失败分类）+ 双 artifact +
  变体语义 + 纪律（fail-closed/内容寻址/行为冻结 parity）+ 扩展点
  （OPT-001/热切换 defer/Ray defer）。
- A2 文档显式映射冻结基线 §8（realizes 什么 / 不触碰什么），不与任何
  冻结正文冲突。
- A3 两 ADR 格式对齐 0016/0017（Context→Decision→Considered Options→
  Consequences），决策可溯源到 PER-005/008 state。
- A4 只提交 4 文件（文档×3 + state）。

## Verification
```yaml
verification:
  level: CONTRACT
  method: 内容核对（A1 清单逐项）+ 与冻结基线 §8 交叉引用核对 + 格式对照
  expected: A1–A4 满足
  actual: >-
    A1 架构文档 §1–§6 覆盖全清单（C# 六组件/provider 内部四件/契约六面/
    双 artifact/变体语义/纪律五条/扩展点五项）；A2 §7 映射 realizes/不触碰/
    依赖方向，零冻结正文改动；A3 两 ADR 四段式对齐 0016/0017，决策溯源到
    PER-005 D1/D3/D9 与 PER-008 D1–D5/grill 记录；A4 精确 4 文件。
  evidence: 本 state
```

## Status log
2026-09-12 · enter→persisted · Human 提出架构级沉淀需求；docs/README 规则
  核实（候选驻 analysis/）；基线 §8 Capability Plane 定位核实。
2026-09-12 · persisted→implemented→closed · 架构候选文档（8 节）+ ADR-0020
  （双 artifact 锚）+ ADR-0021（黑盒边界与变体语义）落地；A1–A4 满足。
