# Unit Test Status

**Project**: UniClaw.Core  
**Version**: Phase 2.3  
**Change**: graph-service-model-separation  
**Task**: 7.4 - Full build + test suite + validation  
**Generated**: 2026-07-15  
**Git Branch**: feature/refactor  
**Git Commit**: 270957d (base; change uncommitted)

---

## Executive Summary

D-28 Graph 层三目录分离（Models/ + Abstractions/ + Services/）+ 接口提取完成。
纯机械重构，零行为变更。全量测试 **670/670 通过**（669 原有 + 1 新增 guard test），0 失败，0 跳过。
`openspec validate graph-service-model-separation` — valid。

| Metric | Value |
|--------|-------|
| Total Tests | **670** |
| Passed | **670** |
| Failed | **0** |
| Error | **0** |
| Skipped | **0** |
| Build | 0 errors |
| Duration | ~0.8s |

**Overall Status**: ✅ PASSED

## Module-Scoped Results (this change)

Data source: `.claude/skills/module-test/contracts/graph_unit.json` (2026-07-15T13:27:32Z, FRESH)

| Scope | Total | Passed | Failed | Notes |
|-------|-------|--------|--------|-------|
| Graph | 40 | 40 | 0 | PlanCompiler/DynamicMatcher/TemplateInstantiator/EntryConfig/TraversalPlan |
| Architecture | 45 | 45 | 0 | includes new `GraphAbstractions_Has4Interfaces` guard |
| Traversal | 113 | 113 | 0 | TraversalEngine interface-typed fields regression-clean |
| **Full suite** | **670** | **670** | **0** | |

## New Guard Test

| Guard | Assertion | Status |
|-------|-----------|--------|
| `DependencyDirectionGuardTests.GraphAbstractions_Has4Interfaces` | `Graph/Abstractions/` = exactly 4 `.cs` 文件 (IDynamicMatcher, IPlanCompiler, ITemplateInstantiator, ITemplateRegistry)，仅 interface 定义，无 class/record/enum/struct | ✅ PASS |

## Design Coverage

Doc: `docs/system/layers/graph.md` (updated with D-28 resolution)

| Class (Services/) | Interface (Abstractions/) | Direct tests |
|-------------------|--------------------------|--------------|
| `DynamicMatcher` | `IDynamicMatcher` | ✅ DynamicMatcherTests |
| `PlanCompiler` | `IPlanCompiler` | ✅ PlanCompilerTests |
| `TemplateInstantiator` | `ITemplateInstantiator` | ✅ TemplateInstantiatorTests |
| `PlaceholderResolver` | — (static utility) | ⚠️ gap (间接经 TemplateInstantiatorTests) |
| `TemplateValidator` | — (static utility) | ⚠️ gap |

**Gap 说明**: PlaceholderResolver/TemplateValidator 无直接单测为既有缺口，非本变更引入（change Non-Goals: 不新增测试，guard test 除外）。

## Cross-Module Contract Aggregation

⚠️ **Data Freshness Warning**: 部分 contract 文件为历史快照，早于本次全量运行（当前全量 670/670 绿是权威状态）。

| Module | Tests (P/F) | Timestamp | Freshness |
|--------|------------|-----------|-----------|
| graph (this change) | 670/0 | 2026-07-15 13:27 | ✅ FRESH |
| simulation-expected-behavior | 665/0 | 2026-07-15 | ✅ FRESH |
| traversal (interface-extraction) | 610/7 | 2026-07-11 | ⚠️ STALE (~4d) — 当时 7 个 pre-existing failures 已在后续变更修复 |
| state_machine | 19/12 | 2026-06-08 | 🔴 VERY STALE (>7d) — 历史快照 |
| v6_9_plan_compilation | 192/0 | 2026-06-07 | 🔴 VERY STALE (>7d) |
| e2e_test | 2/1 | 2026-06-06 | 🔴 VERY STALE (>7d) |
| simulation | 28/0 | 2026-06-06 | 🔴 VERY STALE (>7d) |
| trace | 123/0 | 2026-06-06 | 🔴 VERY STALE (>7d) |

历史 contract 中的 failed 计数不代表当前状态；当前全量 suite 0 failures。

## Code Changes

| File | Change |
|------|--------|
| `Graph/Abstractions/IDynamicMatcher.cs` | New — Match + MatchAll |
| `Graph/Abstractions/IPlanCompiler.cs` | New — Compile |
| `Graph/Abstractions/ITemplateInstantiator.cs` | New — Instantiate |
| `Graph/Abstractions/ITemplateRegistry.cs` | Moved from Models/Template.cs, namespace → .Graph.Abstractions |
| `Graph/Models/MatchableItem.cs` / `MatchResult.cs` | New — 从 DynamicMatcher.cs 拆出 (接口参数类型留 Models/) |
| `Graph/Models/Template.cs` | 仅保留 Template record (4 类型 → 1) |
| `Graph/Services/DynamicMatcher.cs` / `PlanCompiler.cs` / `TemplateInstantiator.cs` | git mv from Models/, namespace → .Graph.Services, 实现对应接口 |
| `Graph/Services/PlaceholderResolver.cs` / `TemplateValidator.cs` | 从 Models/Template.cs 拆出 |
| `Traversal/TraversalEngine.cs` | `_matcher`/`_instantiator` 字段类型 → IDynamicMatcher/ITemplateInstantiator (默认 new 具体类) |
| `tests/.../ArchitectureGuardTests.cs` | +GraphAbstractions_Has4Interfaces guard |
| `tests/.../GraphTests.cs` | +using Graph.Services |
| `docs/system/layers/graph.md` | §1 三目录结构 + 接口清单表 |
| `docs/system/decisions/log.md` | D-28 Deferred → Fixed (归属决策 + guard 记录) |

## Conclusions

- ✅ 28/28 tasks 完成，纯机械分离，零行为变更（670 全绿证明）
- ✅ Abstractions/ 4 接口 guard 锁定（CI-blocking）
- ✅ 耦合约束满足: Models ↛ Abstractions/Services; Abstractions → Models; Services → Abstractions + Models
- ⚠️ 既有缺口: PlaceholderResolver/TemplateValidator 无直接单测（可在后续 change 补齐）
- Ready for archive
