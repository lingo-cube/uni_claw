# Design: Simulation Test Quality Hardening — 完备性可证明 (实际 / 应该 / 证明)

> **创建时间**: 2026-07-16
> **状态**: 设计阶段
> **来源**: brainstorming `simulation-test-quality-hardening`
> **主题**: 把仿真基线测试的「应该遍历什么」变成真全集,「怎么证明做了」变成精确 set-diff

---

## 0. TL;DR

当前仿真基线测试 703 全绿,但「应该遍历什么」对滚动场景**结构性欠计数**:

- `auto_derive` + `WithFixtureDerivation(fixture)` 只从 `StateFixture`(静态 JSON chrome)派生全集。
- 滚动场景的元素由 `PagedItemGenerator` 在 `SimulatedScreen` 上**动态生成**(如 hierarchy 场景 25+30+20=75 项),**不在 fixture 里**。
- 结果:这 75 项对「应该遍历什么」完全不可见,`element_coverage` 永远校验不到它们。
- 校验又是 ratio-based(`ratio >= 0.85`,只报 `"X/Y (Z%)"`,无 diff),且 ratio 压在欠计数的全集上。

**一个完全忘记滚动的引擎仍能通过 `element_coverage`** —— 这正是要堵的洞。

本设计做两件事:

1. **「应该遍历什么」变真全集** = fixture chrome ∪ 完整枚举的生成器内容(确定性模型可直接算出,不必跑)。
2. **「怎么证明做了」变精确 set-diff** = `matched / missed / extra`,严格度按 plan 语义分流(exhaustive=exact,terminating=过游走 guard)。

---

## 1. 承重发现:派生全集不含滚动元素

### 1.1 当前数据通路

```
StateFixture (静态 JSON: N 页 chrome)        ← WithFixtureDerivation 只读这个
        │
        ▼
SimulatedScreen(fixture)
   .WithScrollablePage("network_list", PagedItemGenerator(totalCount:25, pageSize:5, ...))   ← 25 项
   .WithScrollablePage("app_list",     PagedItemGenerator(totalCount:30, pageSize:5, ...))   ← 30 项
   .WithScrollablePage("perm_list",    PagedItemGenerator(totalCount:20, pageSize:5, ...))   ← 20 项
                                                                                共 75 个动态元素
```

引用:
- 测试搭法: [HierarchyBaselineTests.cs:75-85](../../tests/UniClaw.Core.Tests/Baseline/HierarchyBaselineTests.cs#L75-L85)
- 派生只读 fixture: [ExpectedBehavior.cs:163-171](../../src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.cs#L163-L171) — `fixture.Pages.Values.SelectMany(p => p.Elements)`
- `ExpectedBehavior/` 全目录对 `SimulatedScreen` / `IScrollContentSource` / `PagedItemGenerator` **零引用**(已 grep 确认)

### 1.2 三元组现状

| 三元组 | 当前状态 | 判定 |
|---|---|---|
| **实际遍历了什么** | `result.ActionHistory` 全量记录(tap/swipe/back + `element_id`);VisitedPages;Trace。引擎确实 tap 了那 75 项,都在历史里。D-74 据此抓过真 bug(sibling branches visited=0 但 all_visited=true) | ✅ 强 |
| **应该遍历什么** | `auto_derive` 从静态 fixture 派生 → 只有 chrome。75 个滚动项缺席 | 🔴 结构性欠计数 |
| **怎么证明做了** | `VerifyElementCoverage` 是 ratio:`ratio >= 0.85`,只报 `"X/Y (Z%)"`,**无 diff**;ratio 还压在欠计数全集上。滚动项的「证明」只剩 `numeric_anchor.action_history_count`(±5%,informational,不阻断 CI) | 🔴 弱,被欠计数放大 |

引用: [ExpectedBehavior.Verify.cs:155-185](../../src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs#L155-L185) — ratio 计算与 `actual = $"{matchedCount}/{requiredElements.Count} ({ratio:P1})"`。

### 1.3 后果

引擎若完全不滚动(tap 了 0 个滚动项),`element_coverage` 照样过 —— required 集里压根没有它们。hierarchy / scroll / long-list 三大场景(占套件大头)的完备性证明,实际只靠「信任引擎自己的 AllChildrenVisited 退出条件 + 一个 informational 数字锚点」。

---

## 2. 数据通路:全集如何派生(零接口改动)

### 2.1 关键洞察

`IScrollContentSource` 已暴露 `TotalCount` / `PageSize` / `GetPage(int)`,且 `GetPage` 内部已应用 `fillRatio`([PagedItemGenerator.cs:49-76](../../src/UniClaw.Core/Simulation/Scroll/PagedItemGenerator.cs#L49-L76) 只返回填充槽位)。

**全集已经可枚举,接口零改动**:枚举 `GetPage(0) .. GetPage(LastPageIndex)` 即得真全集(含稀疏留空的真实效果)。确定性模型自描述(`totalCount=25/pageSize=5/fillRatio=1.0/namePrefix="Network_"`)→ 全集 = 25 项 `Network_0..Network_24`,可证明完备,**不必跑引擎**。

### 2.2 暴露全集给 derivation

测试侧本就持有 `SimulatedScreen`,所以:

- `SimulatedScreen` 加 `GetScrollableUniverse()` → 枚举所有注册 source 的 `GetPage(0..LastPageIndex)`,返回 `IEnumerable<(string PageId, string ElementId, string Text)>`。
  - `LastPageIndex` 计算已存在: [SimulatedScreen.cs:216-223](../../src/UniClaw.Core/Simulation/Scroll/SimulatedScreen.cs#L216-L223)(private,提为 internal 或在 `GetScrollableUniverse` 内复用)。
- 新增 `ExpectedBehavior.WithDerivation(StateFixture fixture, SimulatedScreen screen)`:
  - 内部合并现有 fixture 派生(page_coverage / element_coverage chrome / collision_proof)+ scroll 全集。
  - `element_coverage.required` = fixture 元素 ∪ 各 source 全集元素。
- 替换当前调用点:`LoadHierarchyExpectedBehavior` 等把 `WithFixtureDerivation(fixture)` 改为 `WithDerivation(fixture, screen)`。

### 2.3 guard 不破

C-5 guard 是「引擎看不到 `SimulatedScreen`」。`SimulatedScreen` 仍只注入 mock vision/action,由**测试**持有并传给 derivation —— 引擎链路不变,guard 不破。

---

## 3. element_coverage schema(C-11 constitution change)

`requiredRatio` 是 masking 的根因。换成精确集合语义:

### 3.1 新 schema

```jsonc
// BEFORE
"elementCoverage": {
  "required": ["auto_derive"],
  "requiredRatio": 0.85
}

// AFTER
"elementCoverage": {
  "mode": "exact",                 // "exact" | "subset" (必填)
  "required": ["auto_derive"],     // 保留,现派生 chrome ∪ scroll 全集
  "allowedMisses": [               // exact 模式下显式豁免,每项必须给 reason
    { "id": "Network_17", "reason": "duplicate-dedup at scroll boundary" }
  ]
}
```

- `exact`: `missed ⊆ allowedMisses` 且 `extra = ∅` 通过。**每个未达元素要么修、要么显式豁免并写明理由** —— 这是「怎么证明做了」的纪律。
- `subset`(terminating 计划): 不做覆盖断言,改过游走 guard(§5)。
- 旧 JSON 无 `mode` → `legacy_ratio` 走旧 ratio 路径(过渡期,标 deprecated;本 change 一次性迁移全部 JSON,过渡分支随后删)。

### 3.2 diff 输出

单一聚合规则 `element_coverage:completeness`,失败信息精确到集合:

```
element_coverage:completeness: FAIL — matched 70/75, missed [Network_17, Network_23, Perm_9, Perm_14, Perm_19], extra []
```

不做每元素一条 `RuleResult` —— 75 条会淹没报告。聚合规则 + 结构化 diff payload(`Actual` 字段放 `matched/missed/extra` 数组)即可。

### 3.2.1 匹配语义:必须改为精确集合运算(self-review 发现)

当前 matcher 用子串 `Contains`([ExpectedBehavior.Verify.cs:166-169](../../src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs#L166-L169)):

```csharp
val?.ToString()?.Contains(reqId) == true   // "Network_1" ⊂ "Network_17" → 误匹配
```

`"Network_1"` 会子串匹配 `"Network_10".."Network_19"`、`"Network_17"` 等 → **当前 ratio 不仅全集欠计数,匹配本身还过计数**。迁到 exact 模式**必须**改为精确集合运算:

- 从 `result.ActionHistory` 提取实际 tap 过的 `element_id` **精确集合**(`HashSet<string>`,等值而非子串)。
- `matched = universe ∩ tappedSet`,`missed = universe − tappedSet`,`extra = tappedSet − universe`。

pre-existing 子串匹配在 exact 下会变成正确性 bug(Network_1 被算作已覆盖 Network_17),必须一并修。

### 3.3 流程: constitution change

`ExpectedBehavior` record 结构变更属 C-11 constitution change flow(见 [CLAUDE.md] 宪章 Guard Tests 段、`docs/system/charter-specification.md` §6)。需同步:
- `ElementCoverageExpectation` record 加 `Mode` + `AllowedMisses` 字段。
- `ExpectedBehaviorDto` + `FromJson` 解析新字段。
- 若有 guard test 锁定 `ElementCoverageExpectation` 形状,一并更新。

---

## 4. 严格度按 plan 语义自动分流

`mode` 不用手填 —— `WithDerivation` 读 `plan.CompletionPolicy?.Type`:

| CompletionPolicy.Type | mode | 理由 |
|---|---|---|
| `TargetFound` | `subset` | 本就该早停,不该断言全集合覆盖 |
| 其他(`AllChildrenVisited` 经 root ExitCondition / `MaxSteps` / `Timeout` / null) | `exact` | 完备遍历,断言全集合 |

分流是真需求:`TargetFound` 下要求 exact 会判一个正确早停的引擎失败。

实现: `WithDerivation` 接收 `TraversalPlan`(或只接收 `CompletionPolicyType?`),据此设 `mode`。JSON 里的 `mode` 字段作为**显式覆盖**(手填优先于自动),默认走自动分流。

---

## 5. 过游走 guard(subset 模式专属)

TargetFound 命中 target 那一步之后,断言后续 action 只能是 `back` / `scroll` / exit,**不得再 tap 新元素**。

- 抓「找到目标却继续乱点」。
- 实现: `VerifyElementCoverage` 在 `mode == subset` 时,定位 ActionHistory 中 target 元素的 tap 位置(`element_id` 含 `CompletionPolicy.TargetName`),其后扫描是否出现新 `element_id` 的 tap。
- 与现有 `completion:target_found`(检查 Success/Reason)正交:一个证「确实停了」,一个证「停对了之后没乱动」。

---

## 6. 迁移:先红,再逐条裁决(预期产出)

### 6.1 全量迁移

~12 个 expected JSON(见 `tests/.../Baseline/Fixtures/expected/`):全量迁移。大多数 full-traversal 场景 → `exact`;target-search 场景 → `subset`。

### 6.2 预期先红

`hierarchy-full-traversal` 从 `0.85` 迁到 `exact` **很可能先红**。该 0.85 当初就是为掩盖 "storage page self-transitions 导致 85.7%" 而校准的(见 `hierarchy-full-traversal.json` 的 `numericAnchor._note`: "Recalibrated 2026-07-14 after navigation-subpage-frames fix (D-74)... element_coverage at 85.7%...")。

**这是设计的预期产出,不是回归**:把欠计数暴露成精确的 `missed: [...]`,然后逐条裁决:

- 是 engine bug → 修引擎。
- 是合理不可达 → 进 `allowedMisses` + 写 reason。

修完之后,「应该遍历什么」才第一次为真。

### 6.3 流程约束

迁移期间测试可红,但**不得用调低阈值/放宽 `allowedMisses` 来强行转绿** —— 每次 `allowedMisses` 新增必须附 reason,且 reason 进 decisions/log。这是「证明」纪律的强制点。

---

## 7. numeric_anchor 降级

element_coverage 变 exact 后,`action_history_count` ±5% 对覆盖证明冗余。

- 保留为 informational 烟雾检查(不删,仍报 INFO/Outside-tolerance)。
- 在 spec/doc 里明确标注「numeric_anchor **不是**完备性证明」,避免日后又被当成主证据。完备性证明的唯一权威 = `element_coverage:completeness` 的 exact 结果。

---

## 8. 改动清单

| 层 | 文件 | 操作 | 内容 |
|----|------|------|------|
| Simulation.Scroll | `SimulatedScreen.cs` | 修改 | 加 `GetScrollableUniverse()`;`LastPageIndex` 提为可复用 |
| Simulation.ExpectedBehavior | `ExpectedBehavior.cs` | 修改 | 加 `WithDerivation(fixture, screen)`(合并 fixture + scroll);`ElementCoverageExpectation` 加 `Mode`/`AllowedMisses` |
| Simulation.ExpectedBehavior | `ExpectedBehavior.cs` (DTO + FromJson) | 修改 | 解析 `mode`/`allowedMisses`;`legacy_ratio` 过渡分支 |
| Simulation.ExpectedBehavior | `ExpectedBehavior.Verify.cs` | 修改 | `VerifyElementCoverage` 改 exact/subset 双路 + 精确 diff + 过游走 guard |
| Tests/Baseline | `HierarchyBaselineTests.cs` / `ScrollableBaselineTests.cs` / `LongListBaselineTests.cs` / `MultiBranchNavigationTests.cs` | 修改 | `WithFixtureDerivation` → `WithDerivation(fixture, screen)`;传 plan 给 mode 分流 |
| Tests/Baseline/Fixtures | `expected/**/*.json` (~12) | 修改 | 全量迁移:`requiredRatio` → `mode`(+ 按需 `allowedMisses`) |
| Constitution | `ArchitectureGuardTests.cs` (若有 ElementCoverage 形状锁) | 修改 | 同步新字段 |

---

## 9. Out-of-Scope (YAGNI,记此)

本设计只立完备性基线。以下次要项先不碰,基线立住后再议:

- OperationRules / TraceIntegrity 维度扩展(新增规则类型)
- numeric_anchor 容差带(±5% 是否放宽/收窄)
- mutation spot-check(注入 engine bug 验证测试是否抓得到)
- 全量 fixture 元素计数审计

---

## 10. 验证

- `dotnet build` 0 错误
- `dotnet test` 迁移后:expected 红项逐条裁决至全绿;最终测试数 ≥ 当前(703)
- 核心断言(人工核):
  - **负向**:故意让 mock 不滚动(删一个 `WithScrollablePage`)→ `element_coverage:completeness` 必须 FAIL,且 `missed` 精确列出缺失项。证明欠计数洞已堵。
  - **正向**:`hierarchy-full-traversal` exact 全绿后,`allowedMisses`(若有)每项有 reason 且进 decisions/log。
  - **分流**:`target-search` 场景 `mode=subset` 不因早停而误判 exact 失败。
  - **过游走**:target 命中后注入一个多余 tap → subset 模式 FAIL。

---

## 11. 风险

| 项 | 风险 | 缓解 |
|----|------|------|
| exact 模式过严,被合理不可达卡死 | 引擎可能对某些元素确实不可达(dedup/popup 遮挡) | `allowedMisses` 豁免 + reason;强制每个 miss 被裁决而非静默放过 |
| schema 改动破坏现有 JSON 解析 | 旧 JSON 无 `mode` 字段 | `legacy_ratio` 过渡分支;本 change 内全量迁移后删过渡 |
| `GetScrollableUniverse` 对无限流(null TotalCount)无界 | 无限流枚举永不终止 | 无限流场景 `mode` 强制 `subset` 或拒绝 exact;`GetScrollableUniverse` 对 null TotalCount 抛 `DomainValidationException`(fail-fast) |
| 派生全集与引擎实际可见集不一致 | 累积 vs 窗口可见性模型差异导致「全集里有的元素引擎视口从未完整呈现」 | 全集是「模型定义的完备集」(应遍历),非「视口曾呈现的」;引擎需滚动到位才能 tap,这正是滚动完备性的证明目标 |
| 迁移工作量被低估 | 12 个 JSON + 4 个测试文件 + 派生/校验重写 | 走 OpenSpec change / opsx:propose 立项;tasks 拆分到文件级 |
