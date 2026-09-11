# WMP-003 — Container Index Count-Stable Fast-Path Invariant Pinning（测试侧不变式固化）
lifecycle_state: closed · disposition: implemented · depth: standard · base: 391fd1a

## Intent（WHAT/WHY）
WMP-002 dogfood（fresh agent）识别出 `WorldRevisionIndex.Create` 的容器索引
快路径（count 相等即整表复用 parent `ContainerPositions`）靠「容器集合
append-only / Matched 只改 basis 不动位置」这一**外部不变式**成立，而非结构
自证。v0.1 无公开路径可构造 count-stable 置换，但代码注释已预告未来
container 终止操作——届时若新增替换/删除流，该快路径会静默 serve stale
position。本 change 把该隐式假设变成显式被测事实：公开接口不变式扫描 +
内部 seam canary，使未来任何触及该分支的修改被迫有意识地处理它。

## Scope
- `tests/UniClaw.Kernel.Tests/WorldModelIndexInvariantTests.cs`（新文件）：
  - 公开不变式扫描：交错 New/Matched/continuity 多 revision 场景，逐对断言
    「容器数不变 ⇒ 容器 identity 序列逐项相同」，并跑 canonical oracle battery。
  - 内部 seam canary（既有 InternalsVisibleTo，不扩任何接口）：构造
    count-stable 置换 revision，**显式断言**当前快路径会 serve stale
    positions——固化「此分支的正确性依赖不变式」这一事实；未来若有人
    修复/加固该分支，canary RED 迫使其有意识更新。
- `tests/UniClaw.Kernel.Tests/WorldModelCanonicalOracleTests.cs` 的**唯一**改动：
  `Battery` 由 `private` 提升为 `internal`（一个关键字，供本 change 的不变式
  测试复用同一 oracle battery，避免复制 40 行逻辑；复用经既有
  InternalsVisibleTo 测试缝，battery 本身仍只消费公共 WorldModel 接口）。
  【2026-09-10 review 修订】初版 Constraints 误写「不改既有测试/文件面仅 1
  个测试文件」，与本 hunk 冲突——属 state 漂移，按 UniFlow A7 修订留痕。
- 本 state + 小 evidence。

## Out of Scope（禁止）
- 禁止生产代码修改（不动 `WorldRevisionIndex` / `WorldModel` / 任何 src/）。
- 禁止把 canary 伪装成公开接口证据（它钉的是内部 seam 行为，注释必须言明）。
- 禁止顺手做 oracle battery Take 采样扩展（另一个独立 follow-up，不混提交）。

## Decisions
- D1 不在生产加快路径防御检查：公开流不可达 + O(n) 校验会吃掉快路径收益；
  以测试固化不变式替代。
- D2 canary 断言「当前会 stale」而非「应该正确」：诚实记录现状契约，未来
  变化时 fail-loud，由修改者决定新契约。

## Acceptance
- A1 公开不变式扫描在交错场景下全绿（count-equal ⇒ identity 序列不变）。
- A2 canary 明确记录快路径在 count-stable 置换下 serve stale position
  （断言通过 = 现状被固化）。
- A3 全量测试通过；clean isolated checkout 全量通过；只提交本 change 精确文件。

## Constraints
- 文件面：1 个新测试文件 + 既有测试文件的 1 处可见性关键字 + state +
  evidence；不改 src/、不改既有测试的任何断言/行为。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >-
    不变式扫描（29 transitions / 27 count-stable / 1 continuity，逐对断言 +
    oracle battery）+ 引用复用 Assert.Same + 内部 canary + 全量 dotnet test
    UniClaw.Kernel.slnx（主树与 clean isolated 精确提交树 f9696b5）+ 双轴 REVIEW
  expected: A1-A3 全满足；canary 如实固化现状；无 src 改动
  actual: >-
    主树与 isolated 均Kernel 279/279 + Agent 17/17；Standards 硬违规（state
    漂移）已按 A7 修订；Spec 缺口（continuity 未入扫描）已补齐
  evidence: evidence/2026-09-10-wmp-003-container-index-invariant.md
```

## Status log
2026-09-10 · enter→understanding · base=391fd1a（含另一会话 PER-004，与本
  change 零文件重叠）；主树 dirty 仅两份既有无关 untracked 文档。
2026-09-10 · understanding→resolved→persisted→planned→implementing · 三测
  落地（公开扫描 / continuity 引用复用 / 内部 canary）；Battery private→
  internal 一个关键字以复用 oracle battery。
2026-09-10 · implementing→reviewing→verifying · Standards 发现 state 漂移
  （Constraints vs 既有测试 hunk）→按 A7 修订留痕；名实不符测试补 Assert.Same；
  Spec 发现 continuity 腿未入扫描→交错补齐（29/27/1）。主树全量 279/279 +
  17/17；isolated 精确树 f9696b5 同绿。
2026-09-10 · verifying→closed · A1-A3 满足；提交 4 文件；无关 dirty 未动。
