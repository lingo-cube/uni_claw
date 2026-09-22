# UIW-005 — 产品 container association realization

lifecycle_state: closed · disposition: none · depth: standard · base: 62041cf3 · Human closure 2026-09-20（批量 closure，GATE-001 台账事件 #12）

## Intent

UWM-009 §10 association seam 的第一个产品实现。HOST-001 前置：自驱
环路必须能铸根容器，产品默认（strategy=null = 不启用容器的旧路径）
做不到（WorldModel:264 门已实证）。2026-09-20 人裁决 b：不造 15 行
临时件，直接落正式零件（推翻 Leader 的 a 建议）。

## Decisions

- D1 匹配规则（确定性，UWM-009 §16 算法不冻结）：当前 claim value 与
  owner signature 约定（`ui.container.signature.<id>` = establishing
  claim 原文，WorldModel:276）**逐字节相等** → Matched；空/空白 claim →
  Insufficient；无容器/无匹配 → New；多匹配 → Ambiguous（本 realization
  内不可达，词汇保留、fail-safe 交 gates）。
- D2 权衡存证：逐字节相等 ⇒ 屏幕内容任何变化（滚动/弹窗/顺序）= 新
  屏幕身份（经 AssociationDecision 留痕可追溯）；归一化签名 /
  occurrence 重叠改进 = 同 seam 后续 realization 迭代。
- D3 公开面 +1（196→197）：KernelRuntimeSurfaceWhitelistTests 经本
  change 授权更新——RUN-003 白名单执法**首次实际拦截并走完授权流程**。
- D4（2026-09-20 装配期修正）：关联只判别屏幕身份 claim
  （`ScreenIdentitySubject = "ui.screen"`）；内容类 claim →
  Insufficient（"not-screen-identity-claim"）——否则每帧内容变化都铸
  新容器，与司机单根容器约束（KernelRunDriver StepAct）冲突。HOST-001
  装配时实证发现；subject 约定属 realization，非协议冻结。

## Verification

```yaml
level: DETERMINISTIC
method: 5 测试（空 claim→Insufficient / 首帧→New evidence-backed /
        同屏重看保持身份 / 变化→新身份且 AssociationDecision 留痕 /
        同输入同 proposal）+ 全量回归
expected: 四分类语义按 D1 成立；WorldModel gates 端到端行为正确
actual: 5/5 通过；全量 592/592（Kernel 420 含白名单 197 GREEN）
evidence: tests/UniClaw.Kernel.Tests/World/ProductAssociationStrategyTests.cs
```

## Status log

- 2026-09-20 · closed·human-closure · 台账事件 #12：人批准关闭（批量，二选二）。
- 2026-09-20 · created·implemented·verified · 单会话轻量道完成
  （台账事件 #10 裁决 b + #11 仿真方针纠正同日）；待批量 closure。
- 2026-09-20 · d4-amended·reverified · D4（subject 判别域收窄）装配期
  落地；测试 7/7（+非屏幕 subject 不判别 / 内容证据不铸容器两条集成）；
  全量 598/598。
