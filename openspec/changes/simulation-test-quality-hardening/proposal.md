## Why

仿真基线测试 703 全绿,但「应该遍历什么」对滚动场景**结构性欠计数**,完备性证明形同虚设:`auto_derive` + `WithFixtureDerivation(fixture)` 只从 `StateFixture`(静态 JSON chrome)派生全集,而滚动元素由 `PagedItemGenerator` 在 `SimulatedScreen` 上动态生成(hierarchy 场景 25+30+20=75 项),**完全不在派生视野内**。叠加 `element_coverage` 的 ratio 阈值(`0.85`)+ 子串 `Contains` 匹配(`"Network_1"` 误匹配 `"Network_17"`,既欠计数又过计数),导致**一个完全不滚动的引擎仍能通过 `element_coverage`**。滚动/层级链路目前没有任何可信的完备性回归护栏,必须现在堵。

## What Changes

- **新增 scroll 全集派生**:`SimulatedScreen.GetScrollableUniverse()` 枚举所有注册 `IScrollContentSource` 的 `GetPage(0..LastPageIndex)`,得到模型定义的真全集(确定性、可证明完备,不必跑引擎);`TotalCount==null`(无限流)时 fail-fast 抛 `DomainValidationException`。
- **新增 `ExpectedBehavior.WithDerivation(StateFixture, SimulatedScreen)`**:合并 fixture chrome ∪ scroll 全集 → `element_coverage.required`;保留 `WithFixtureDerivation(fixture)` 用于无滚动场景与过渡期共存。
- **BREAKING — `ElementCoverageExpectation` schema 变更(C-11 constitution change)**:移除 `RequiredRatio`;新增 `Mode`(`exact` | `subset` | `legacy_ratio`)与 `AllowedMisses`(exact 模式下显式豁免,每项带 `Id` + `Reason`)。旧 JSON 无 `Mode` 时走 `legacy_ratio` 过渡分支(本 change 内全量迁移后删除)。
- **`VerifyElementCoverage` 改精确 set-diff**:`matched = required ∩ tapped` / `missed = required − tapped` / `extra = tapped − required`;exact 通过条件 = `missed ⊆ AllowedMisses.Ids` 且 `extra = ∅`;失败信息精确列出 `missed`/`extra`(非百分比)。
- **匹配语义修正(既有 bug 一并修)**:element 匹配从子串 `Contains` 改为对 `element_id` 的**精确集合等值**(`HashSet<string>`)。
- **严格度按 plan 语义自动分流**:`CompletionPolicy.Type == TargetFound` → `subset`(过游走 guard:target 命中后禁新 tap);其余 → `exact`。JSON `mode` 为显式覆盖,缺省自动分流。
- **~12 个 expected JSON 全量迁移**:`requiredRatio` → `mode`。迁移期预期先红(把被 ratio 掩盖的欠计数暴露成精确 `missed`),逐条裁决——engine bug 修引擎,合理不可达进 `AllowedMisses` + reason 并记入 decisions/log。
- **`numeric_anchor` 语义降级**:显式标注为 informational 烟雾检查,**明确不再作为完备性证明**;唯一完备性权威 = `element_coverage:completeness` 的 exact 结果。

## Capabilities

### New Capabilities

(无 —— 本次为既有验证契约的硬化,不引入新能力域。)

### Modified Capabilities

- `expected-behavior`: 验证契约硬化。**MODIFIED** 三条 Requirement —— `ElementCoverageExpectation`(schema: 移除 `RequiredRatio`,加 `Mode`/`AllowedMisses`;语义: ratio→精确 set-diff,exact/subset/legacy 三路;匹配: 子串→精确等值)、`NumericAnchor`(降级为 informational,明确非完备性证明)、`Verify`(匹配语义按维度分流:element_coverage 改精确集合,page_coverage/collision_proof 仍语义匹配);**ADDED** 一条 `WithDerivation(fixture, screen)` Requirement(派生源从 StateFixture-only 扩展为 fixture ∪ scroll 全集)。

## Impact

- **代码**:
  - `src/UniClaw.Core/Simulation/Scroll/SimulatedScreen.cs` — 加 `GetScrollableUniverse()`;`LastPageIndex` 提为可复用 internal helper。
  - `src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.cs` — 加 `WithDerivation(fixture, screen)`;`ElementCoverageExpectation` 加 `Mode`/`AllowedMisses`,移除 `RequiredRatio`;DTO + `FromJson` 解析新字段 + `legacy_ratio` 过渡。
  - `src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs` — `VerifyElementCoverage` 重写(exact/subset/legacy 三路 + 精确 diff + 过游走 guard + 精确集合匹配)。
  - `tests/.../Baseline/{Hierarchy,Scrollable,LongList,MultiBranch}BaselineTests.cs` — 调用点 `WithFixtureDerivation` → `WithDerivation(fixture, screen)`,传 plan 给 mode 自动分流。
- **数据**:`tests/.../Baseline/Fixtures/expected/**/*.json`(~12)全量迁移。
- **Schema 契约**:C-11 constitution change flow;`ArchitectureGuardTests.cs` 若锁 `ElementCoverageExpectation` 形状需同步。
- **依赖**:无新增外部依赖;`IScrollContentSource` 接口零改动(已暴露 `TotalCount`/`PageSize`/`GetPage`);C-5 guard 不破(`SimulatedScreen` 仅测试侧持有,引擎链路不变)。
- **预期回归**:迁移后若干 full-traversal 场景先红(`hierarchy-full-traversal` 的 `0.85` 原本为掩盖 storage page self-transitions 的 85.7%),属设计预期产出,逐条裁决后转绿。
