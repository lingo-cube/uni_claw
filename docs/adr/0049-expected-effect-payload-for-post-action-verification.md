# ADR-0049：预期效果载荷（Expected Effect）作为 post-action 验证对照物

状态：accepted（2026-10-12，协议修复 grill——记录见 `docs/design/protocol-completeness-formal-audit-v0.1.md` 附录 C；实现经 PROTOCOL-FIX）

「Verified Effect」的语义是"与**预期 effect** 一致的改变"，但"预期"至今无载体：
状态型期望散落在 TargetSpec 断言里，变化型期望（滚动）被硬编码为
`PostActionEffectVerificationInput` 的三个特设布尔（PriorRoute / CurrentRoute /
ScrollContentChanged）——形式审计 D8 判定为字段级缺口（与 roadmap G6
"expected change correlation" 同源）。

## Decision

### 1. 验证输入侧独立期望载荷

`PostActionEffectVerificationInput` 增加显式期望载荷，承载**两族封闭谓词**：

```text
StateBecomes(subject, value)          状态型：目标应处于何态
ChangePredicate(封闭起步词汇)：        变化型：应发生何种改变
  ContentChanged                        可见 occurrence 内容集发生变化
  RouteStable                           路由保持不变
```

词汇封闭起步、可经协议变更扩展（通则 4：锁类别不声明 exhaustive）。

### 2. 边界

- 期望属 **authoring/意图侧投影**（CONTEXT「Expected Effect」词条已落）：不是
  canonical effect 语义、不进 CanonicalBinding、不进 Execution Contract、不是
  第二套 effect 权威。
- 滚动三布尔**迁移为** `RouteStable ∧ ContentChanged` 的实例化，删除特设字段。

## Consequences

- D8 闭合：验证永远有显式对照物；新增 effect 类型不再付"每次硬编码一次期望"的税。
- D6 缓解增强：判定对照的是显式期望谓词，而非 prior 染色的身份连续。

## Rejected alternatives

- **并入 TargetSpec（仅状态断言）**：表达不了变化型（滚动/滑动无终态）。
- **维持特设布尔**：每加一种 effect 改一次输入形状，已两次付税（滚动、AGT-005）。
- **进 CanonicalBinding / Contract**：会把验证对照物变成授权语义的一部分，越权。

## Known gaps

- 谓词词汇表首版（StateBecomes / ContentChanged / RouteStable）随 PROTOCOL-FIX
  切片落地；布尔迁移需同步锚定测试。
