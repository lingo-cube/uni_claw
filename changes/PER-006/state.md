# PER-006 — CONTEXT.md「Perception Acquisition」词条（自 PER-005 Scope ⑦ 剥离）
lifecycle_state: persisted · disposition: none · depth: standard · base: 6d7eb2a4

## Intent（WHAT/WHY）
PER-005 落地了感知 acquisition 的真实 provider（截屏→服务→proposal 全链），
但 `CONTEXT.md` 尚无「Perception Acquisition」词条——采集能力（拿像素/产
raw artifact）与 Perception 解释能力（看懂像素）的域区分未被词汇化，容易被
误读为同一概念。本 change 只补词条（PER-004 先例：词汇锁定是独立 change
单元）。

## Scope
- `CONTEXT.md` 新增「Perception Acquisition」一个词条：Capability Plane
  观察侧的采集能力（设备 → RawArtifact，宿主侧 realization；transport
  形态 uds/tcp/loopback 等 realization 细节**不进**词条——glossary 只定义
  WHAT）。
- 本 state。

## Out of Scope（禁止）
- 不改产品代码/测试；不重开 PER-004 已锁的 Perception / Fast-Slow 词条；
- 不加 transport、JPEG/RGBA、编码器等实现细节；不动 ADR。

## Decisions
- D1 剥离自 PER-005 Scope ⑦（A7 范围手术，PER-005 state 留痕）：并发会话
  UAR-001 活跃返工持有 CONTEXT.md，释放时点不可预期；PER-005 代码与验收
  已全部完成，不为纯文档项挂起。

## Acceptance
- A1 `CONTEXT.md` 恰有一个「Perception Acquisition」词条，锁定采集≠解释
  的域区分与非 Authority 边界，无实现细节（禁词：uds/tcp/jpeg/png/编码）。
- A2 变更文件面仅 `CONTEXT.md` + 本 state；既有词条（含 PER-004 两条）零
  改动。

## Constraints
- 执行前置：并发会话（UAR-001）CONTEXT.md 改动落地（提交或放弃），施工区
  释放——RVR-002 F1 clobber 避让同款。

## Verification
```yaml
verification:
  level: CONTRACT
  method: rg 词条计数 + 新词条区间禁词扫描 + exact-path status（对齐 PER-004 验法）
  expected: A1–A2 满足
  actual: pending（前置未满足）
  evidence: 本 state
```

## Status log
2026-09-12 · enter→persisted · 自 PER-005 Scope ⑦ 剥离建账；等待 CONTEXT.md
  施工区释放（UAR-001 活跃返工中）。
