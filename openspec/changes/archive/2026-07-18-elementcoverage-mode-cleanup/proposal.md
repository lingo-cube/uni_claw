## Why

`simulation-test-quality-hardening` (D-86) introduced the `element_coverage` 精确 set-diff schema (`Mode` exact/subset + `AllowedMisses`) and a `legacy_ratio` transitional path for coexistence during JSON migration. That migration is now complete: all 16 active expected-behavior JSONs carry explicit `mode` (exact/subset); the ratio *verify* path is dormant for every active scenario (the loophole is closed). The transitional residue — `LegacyRatio` enum member, `RequiredRatio` field/DTO, `VerifyElementCoverageLegacy`, and the Mode auto-derive that overloads `LegacyRatio` as an internal "mode absent" placeholder — is now dead weight that complicates the schema. This change removes it for a clean `Mode ∈ {exact, subset}` contract, executing the removal the merged spec already mandates ("SHALL be removed once all expected-behavior JSON files migrate to exact/subset").

## What Changes

- **Remove `LegacyRatio`** from `ElementCoverageMode` → enum is `{Exact, Subset}`. `Mode` defaults to `Exact` when absent from JSON (safe default; no ratio fallback).
- **Remove `RequiredRatio`** — field on `ElementCoverageExpectation`, DTO property, and `FromJson` mapping. The ratio-threshold loophole is gone at the type level.
- **Remove `VerifyElementCoverageLegacy`** (the ratio + substring-`Contains` verify path) and the switch default that routed to it.
- **Remove Mode auto-derive** — `WithDerivation`/`WithFixtureDerivation` no longer overload `LegacyRatio` as a "mode absent" signal to derive Mode from `CompletionPolicy.Type`. All active JSONs have explicit `mode`, so auto-derive is dead. `CompletionPolicy` remains a derivation param, used only to capture `TargetName` for subset's over-traversal guard.
- **Migrate 4 unreferenced legacy orphan fixtures** (`persistent-dedup`, `overlapping-adaptive`, `wifi-list-full-traversal`, `wifi-list-target-search`) — add explicit `mode`, drop `requiredRatio`. Not loaded at runtime, but migrated for schema consistency (no `requiredRatio` remains anywhere).
- **Spec** (`expected-behavior`): `Mode` becomes `exact` | `subset` (required; defaults to `exact` if absent); remove the `legacy_ratio` Mode value, the legacy transitional scenario, the Mode auto-derive behavior, and the `RequiredRatio` mention.

## Capabilities

### New Capabilities

(无 —— 纯清理, 不引入新能力。)

### Modified Capabilities

- `expected-behavior`: `ElementCoverageExpectation` schema 收敛 — `Mode` 移除 `legacy_ratio` 值 (剩 exact/subset, 缺省 exact), 移除 `RequiredRatio` 字段; `WithDerivation` 移除 Mode auto-derive (Mode 现为 JSON 显式, 不再据 CompletionPolicy 分流); 移除 legacy_ratio 过渡场景与 auto-derive 场景。`CompletionPolicy` 参数保留仅供 subset 捕获 TargetName。

## Impact

- **代码**:
  - `src/UniClaw.Core/Simulation/ExpectedBehavior/ElementCoverageMode.cs` — 移除 `LegacyRatio`。
  - `src/UniClaw.Core/Simulation/ExpectedBehavior/ElementCoverageExpectation.cs` — 移除 `RequiredRatio` 参数; `Mode` 缺省改 `Exact`。
  - `src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.cs` — `FromJson`/DTO 移除 `RequiredRatio`; 移除 `ParseElementCoverageMode` 的 legacy 分支; `DeriveElementCoverage`/`ResolveModeAndTarget` 移除 auto-derive。
  - `src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs` — 移除 `VerifyElementCoverageLegacy` + switch default。
- **数据**: 4 个 orphan fixture (`scroll/persistent-dedup.json`, `scroll/overlapping-adaptive.json`, `scroll/wifi-list-full-traversal.json`, `scroll/wifi-list-target-search.json`) 加 `mode` 删 `requiredRatio`。
- **Schema 契约**: C-11 constitution change flow; `Mode` enum 值数变更 (3→2) — `ElementCoverageMode` 不在 `locked-enums.md`, 无 guard 锁定。
- **依赖/回归**: 无新依赖; 711 测试为安全网 (含 8 个 element_coverage 负向测试); 无 active JSON 依赖 auto-derive 或 legacy_ratio, 预期零行为变化。
