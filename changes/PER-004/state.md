# PER-004 — Perception 与 Fast/Slow realization 边界锁定
lifecycle_state: closed · disposition: none · depth: standard · base: 687b6e7b

## Intent（WHAT/WHY）
现有产品基线、协议基线与 UWM-009 已分别声明 Capability Plane、P2 和 Fast/Slow
策略边界，但 `CONTEXT.md` 缺少可直接检索的 Perception 领域词条，导致
`FastPerception` 容易被误读为顶层产品组件或跨组件依赖。本 change 只把既有
权威收敛为 canonical glossary 语义，不创建新 Authority 或实现承诺。

## Scope
- `CONTEXT.md` 新增 `Perception` 与 `Fast / Slow Perception` 两个词条。
- 锁定 Capability、realization、P2 出面与非 Authority 边界。

## Out of Scope
- 产品代码、测试、程序集与可见性修改。
- 新建 `IPerception`、修改 `FastPerception` / strategy interface 或确定
  public/internal 形状。
- Fast/Slow 调度、算法、阈值、live acquisition、failure result、artifact
  形状或 Observation Control 外部协议边。
- 修改冻结的架构/协议正文或新增 ADR。

## Decisions
- Perception 是 Capability Plane 的 typed capability，不是 L1/L2 Owner 或
  canonical Authority。
- Fast / Slow 仅是 Perception 内部 realization category，不是顶层产品组件、
  跨组件协议参与者或 authority class。
- Perception 跨 owner 的观察输出统一经 P2 `ObservationProposal` 出面；UIWorld、
  Agent、Control、Assurance 与 Effect Boundary 不依赖具体 Fast/Slow realization。
- Fast/Slow 选择与组合属于 Observation Control / Perception 内部策略；具体
  interface、代码可见性和 module 布局等待真实 buyer 后另立 change。
- 本次是既有权威消歧，不满足新 ADR 的必要性。

## Acceptance
1. `CONTEXT.md` 恰有一个 `Perception` 词条和一个 `Fast / Slow Perception` 词条。
2. 两词条明确 Capability Plane、P2、realization 与非 Authority 语义，并列出
   防止顶层化/直连 owner 的 Avoid 词汇。
3. `CONTEXT.md` 新词条不出现 `IPerception` 签名、public/internal、缓存、
   具体模型或调度算法。
4. 变更文件面仅为 `CONTEXT.md` 与 `changes/PER-004/state.md`；产品代码与测试零改动。

## Constraints
词条保持 glossary 体例，只定义 WHAT，不记录实现 HOW；不重开 UWM-009 §16、
产品基线 §8 或协议 P2。

## Verification
```yaml
verification:
  level: CONTRACT
  method: rg 精确计数与禁词扫描 + git diff --check + exact-path diff 文件面核对
  expected: Acceptance 1–4 全满足，Markdown diff 无格式错误
  actual: >
    Perception / Fast-Slow 两词条精确计数均为 1；新词条区间禁词扫描为 0；
    git diff --check 通过；exact-path status 仅 M(CONTEXT.md) +
    ??(changes/PER-004/state.md)，产品代码、测试、ADR 与冻结基线零触碰。
  evidence: >
    rg -c 两条词头；sed 限定词条区间后 rg 禁词；git diff --check --
    CONTEXT.md；git status --short -- CONTEXT.md changes/PER-004/state.md
```

## Status log
2026-09-10 · understanding→resolved · 复验产品基线 §8、协议 P2、UWM-009 §16、
  CONTEXT.md 与当前代码调用面；确认缺口是 glossary 消歧，不是新接口 buyer
2026-09-10 · resolved→persisted→planned · Human 明确授权直接落地；范围锁为
  CONTEXT 两词条 + 本 Change State，代码/ADR/冻结基线零触碰
2026-09-10 · planned→implemented · CONTEXT.md 新增 Perception 与 Fast / Slow
  Perception 两个 canonical 词条，锁定 Capability/P2/realization/非 Authority
2026-09-10 · implemented→reviewed · 逐句对照产品基线 §8、协议 P2、UWM-009 §16；
  删除具体类名式 Avoid，未引入实现形状或第二套协议
2026-09-10 · reviewed→verified→closed · 两词条唯一、禁词扫描零命中、
  Markdown diff check 通过、精确文件面合规 → PERCEPTION_REALIZATION_BOUNDARY_LOCKED
