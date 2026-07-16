## Context

完整背景与逐项改动见 [docs/refactor/2026-07-16-simulation-test-quality-hardening-design.md](../../../docs/refactor/2026-07-16-simulation-test-quality-hardening-design.md)。本文为架构决策摘要。

完备性证明存在两端缺口:

- **「应该遍历什么」欠计数**: `WithFixtureDerivation(fixture)` 只读 `StateFixture.Pages`(静态 JSON chrome)。滚动元素由 `PagedItemGenerator` 在 `SimulatedScreen` 上动态生成(hierarchy 场景 75 项),`ExpectedBehavior/` 全目录对这些零引用(grep 确认)。这 75 项对「应该遍历什么」完全不可见。
- **「怎么证明做了」过松**: `VerifyElementCoverage` 是 `ratio >= RequiredRatio`,只报 `"X/Y (Z%)"` 无 diff;matcher 用子串 `Contains`(`"Network_1"` 误匹配 `"Network_17"`)—— 不仅欠计数还过计数。

后果:引擎若完全不滚动(tap 了 0 个滚动项),`element_coverage` 照样过 —— required 集里压根没有它们。

**约束**: C-5(引擎不得见 `SimulatedScreen`);C-11(ExpectedBehavior schema 变更走 constitution change);确定性模型原则(`PagedItemGenerator.GetPage` 是 pageIndex 纯函数)。

## Goals / Non-Goals

**Goals:**

- 「应该遍历什么」= fixture chrome ∪ 完整枚举的 scroll 内容,从确定性模型定义派生,**可证明完备**(不必跑引擎)。
- 「怎么证明做了」= 精确 set-diff(`matched/missed/extra`),失败可定位到具体元素。
- 严格度按 plan 语义分流: 完备遍历=exact,TargetFound=过游走 guard。
- 暴露被 ratio 掩盖的真实欠计数(预期先红),逐条裁决后转绿且每个豁免有 reason。

**Non-Goals:**

- OperationRules / TraceIntegrity 维度扩展。
- numeric_anchor 容差带调整(仅降级语义角色,不改算法)。
- mutation spot-check、全量 fixture 计数审计。
- 真机/Mode A 链路(Phase 3,另立 change)。

## Decisions

### D-1: 全集从「模型定义」派生,不从「引擎观测」派生

**选择**: 全集 = fixture 元素 ∪ 枚举 `IScrollContentSource.GetPage(0..LastPageIndex)` 得到的元素。

**为何不选「引擎在 trace 中看到过的元素」**: 那测的是「被暴露的」而非「模型定义的完备集」。引擎若从不滚到第 4 页,就永远「看不到」第 16-20 项 → 欠滚动反而被掩盖。这正是要抓的失效模式,不能用观测集当尺子。

**为何零接口改动可行**: `IScrollContentSource` 已暴露 `TotalCount`/`PageSize`/`GetPage(int)`,且 `GetPage` 内部已应用 `fillRatio`(只返回填充槽位)。全集已可枚举,确定性模型自描述 → 可证明完备。

### D-2: `SimulatedScreen.GetScrollableUniverse()` 暴露给测试侧 derivation

**选择**: `SimulatedScreen` 加 `GetScrollableUniverse()`,测试调用 `WithDerivation(fixture, screen)` 合并。

**为何不破 C-5**: C-5 约束的是「引擎不得见 `SimulatedScreen`」。`SimulatedScreen` 仍只注入 mock vision/action;derivation 由**测试**调用,引擎链路不变。

**为何新增 `WithDerivation` 而非改 `WithFixtureDerivation` 签名**: 保持 `WithFixtureDerivation` 对无滚动场景可用(向后兼容,过渡期共存)。

### D-3: schema `Mode` 取代 `RequiredRatio`(C-11 BREAKING)

**选择**: `ElementCoverageExpectation` 加 `Mode`(`exact` | `subset` | `legacy_ratio`)与 `AllowedMisses`(exact 模式显式豁免,每项带 `Id` + `Reason`)。

**为何保留 `legacy_ratio` 过渡分支**: 一次性迁移全部 JSON 期间,未迁移文件仍需可解析。迁移完成后删除。

**为何不保留 ratio 作为 exact 的软模式**: ratio 是 masking 的根因(85% 掩盖了 storage self-transition 的 85.7%)。保留它等于保留漏洞。`AllowedMisses` + reason 是「显式、可审计的豁免」,与 ratio 的「隐式放宽」语义对立。

**替代方案被否决**: B(只改 diff 不改全集源)—— 75 个滚动项仍未被校验,治标不治本;C(全集=引擎观测)—— 见 D-1,会掩盖欠滚动。

### D-4: exact 通过条件 = `missed ⊆ AllowedMisses` 且 `extra = ∅`

**选择**: `extra`(tap 了全集外的元素)必须为空;`missed` 必须全部在 `AllowedMisses` 中且每项有 reason。

**为何要求 extra=∅**: 抓「幽灵 tap」(引擎 tap 了模型里不存在的元素),否则覆盖率达 100% 仍可能有垃圾动作。`back_button`/`readonly` 已在派生时排除,不产生假 extra。

### D-5: mode 按 `CompletionPolicy.Type` 自动分流

**选择**: `TargetFound` → `subset`(过游走 guard);其余(`AllChildrenVisited` 经 root ExitCondition / `MaxSteps` / `Timeout` / null)→ `exact`。JSON `mode` 为显式覆盖,缺省自动分流。

**为何不全部 exact**: TargetFound 本就该早停,要求 exact 会判正确早停的引擎失败。

### D-6: subset 模式 = 过游走 guard

**选择**: 定位 target 元素 tap 位置(`element_id` 含 `CompletionPolicy.TargetName`),其后扫描不得出现新 `element_id` 的 tap(只允许 back/scroll/exit)。

**与 `completion:target_found` 正交**: 一个证「确实停了」,一个证「停对后没乱动」。

### D-7: 匹配语义从子串 `Contains` 改精确集合运算

**选择**: 从 `result.ActionHistory` 提取实际 tap 过的 `element_id` 精确 `HashSet<string>`(等值),做集合运算。

**为何必须改**: 子串匹配下 `"Network_1"` 匹配 `"Network_17"`,exact 模式会变成正确性 bug。pre-existing 漏洞一并修。

### D-8: 无限流(null TotalCount)拒绝 exact

**选择**: `GetScrollableUniverse()` 对 `TotalCount == null` 抛 `DomainValidationException`(fail-fast);无限流场景 `mode` 须 `subset`。

**为何**: 无限流枚举无界,exact 无意义。

## Risks / Trade-offs

- **[exact 过严,合理不可达卡死]** → `AllowedMisses` + reason 豁免;强制每个 miss 被裁决而非静默放过;reason 进 decisions/log。
- **[schema 改动破坏旧 JSON 解析]** → `legacy_ratio` 过渡分支;本 change 内全量迁移后删。
- **[派生全集与引擎实际可见集不一致(累积 vs 窗口可见性)]** → 全集是「模型定义的完备集」(应遍历),非「视口曾呈现的」;引擎须滚动到位才能 tap,这正是滚动完备性的证明目标,不是 bug。
- **[迁移工作量被低估]** → tasks 拆到文件级;先迁最简场景建立流程,再迁 hierarchy(最可能先红)。
- **[无限流无界枚举]** → D-8 fail-fast。
- **[trade-off: `legacy_ratio` 临时增加代码复杂度]** → 接受,换取零停机迁移;迁移完成即删。

## Migration Plan

1. 实现 `GetScrollableUniverse` + `WithDerivation` + 新 schema(含 `legacy_ratio` 分支)。
2. 重写 `VerifyElementCoverage`(exact/subset/legacy_ratio 三路 + 精确匹配)。
3. 全量迁移 ~12 个 expected JSON:`requiredRatio` → `mode`(按 plan 语义自动分流,JSON 不必手填 mode)。
4. 跑测试: 预期 full-traversal 场景先红 → 逐条裁决(missed 列表)。
5. 裁决结果: engine bug 修引擎;合理不可达进 `AllowedMisses` + reason → decisions/log。
6. 全绿后删除 `legacy_ratio` 过渡分支。
7. 同步 C-11 guard test(`ArchitectureGuardTests.cs`)与四层文档(`layers/simulation-baseline.md`)。

**回滚**: 若 exact 在某场景无法收敛,migration 可分文件回退(单 JSON 改回 `legacy_ratio`),不影响其余。过渡分支保留至全部收敛。

## Open Questions

- (待迁移时确认) `hierarchy-full-traversal` 的 `85.7%` 欠计数根因是「storage page self-transitions」(fixture 特性)还是 engine bug?迁移到 exact 后据 `missed` 列表裁决。
- (低优) `AllowedMisses` 是否需要一个上限(防止滥用豁免)?初版不加上限,靠 reason + decisions/log 审计约束;若滥用再议。
