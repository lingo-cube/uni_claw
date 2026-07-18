## Context

`simulation-test-quality-hardening` (D-86) 留下一条 deferred 的 §8.1: `legacy_ratio` enum 成员 + `RequiredRatio` 字段 + `VerifyElementCoverageLegacy` + Mode auto-derive 的彻底删除。当时推迟因有二: (1) ratio 验证路径已对所有 active 场景 dormant; (2) auto-derive 用 `LegacyRatio` 作 "mode absent" 占位, 删除它需先解耦 auto-derive。本变更完成该清理 —— 所有 active JSON 已迁移到显式 mode, auto-derive 是死代码, 可一并删除。

## Decision

**删除 `LegacyRatio` + `RequiredRatio` + auto-derive, 不保留任何 ratio 回落。** `Mode` 缺省 → `Exact` (安全缺省, 非 ratio)。

**为何删除 auto-derive 而非保留**: auto-derive 需一个 "mode absent" 信号, 原本复用 `LegacyRatio` 占位。删除 `LegacyRatio` 后, 保留 auto-derive 需另引入 nullable/显式标记 → 重新引入复杂度, 仅为支撑一个无 active JSON 依赖的死特性。所有 16 个 active fixture 都显式写 `mode`, 无文件省略 mode → auto-derive 零调用。删之, schema 收敛到 `Mode ∈ {exact, subset}` 显式契约。

**`CompletionPolicy` 参数保留**: `WithDerivation`/`WithFixtureDerivation` 的 `CompletionPolicy?` 参数不再用于 Mode 分流, 仅用于 subset 模式捕获 `TargetName` (过游走 guard 据此定位 target tap)。这是 subset guard 的刚需, 与 auto-derive 无关。

**4 个 orphan fixture 一并迁移**: `persistent-dedup`/`overlapping-adaptive`/`wifi-list-full-traversal` (→exact) + `wifi-list-target-search` (→subset)。运行时不被加载, 迁移仅为 schema 一致性 (全仓无 `requiredRatio` 残留)。

## Risks / Trade-offs

- **[auto-derive 删除破坏 spec 场景]** → 原 "Mode auto-derived when absent" 场景被 "absent Mode defaults to exact" 替代。无 active 依赖, 无测试覆盖丢失 (8 个负向测试均显式构造 Mode)。
- **[orphan fixture 迁移误判 mode]** → orphan 运行时不加载, mode 误判零影响; 按 scenario 语义 (full-traversal→exact, target-search→subset) 赋值。
- **[enum 值数变更]** → `ElementCoverageMode` 3→2 值, 不在 `locked-enums.md`, 无 guard 锁定, C-11 flow 已走本 change。

## Migration Plan

1. 删 code (enum/record/DTO/FromJson/verify/auto-derive)。
2. 迁移 4 orphan fixture。
3. `openspec validate` + 全量测试 (711 green 期望, 零行为变化)。
4. archive + 决策记 D-88 + commit/push。

**回滚**: 纯删除型变更, 若 exact/subset 路径出现意外, git revert 单 commit 即可。
