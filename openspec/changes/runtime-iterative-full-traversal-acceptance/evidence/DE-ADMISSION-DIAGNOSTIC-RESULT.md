# PROJECT_LEADER_SETTINGS_SEMANTIC_CAPABILITY_DE_ADMISSION_DIAGNOSTIC_RESULT

> Gate: DIAGNOSIS_ONLY · 2026-08-29 · 零代码修改

## 1. Current Residual Scope

D: `row_001 / 'Settings'` — 页面标题 text_block，seq 2 出现一次
E: `row_012 / 'Dark theme, font size...'` — 副标题 text_block，seq 4/5/10/11 出现

## 2. row_001 'Settings' Evidence Chain

### 全链路追踪（源码级）

```
① PhysicalEnvironment.ObserveAsync()
   → ObservedElement('Settings', type='text_block', index=N, bounds=[...])

② SemanticObservationFactProjector.Project(observation)
   → SemanticObservationFact(occurrence=SHA256(sourceId,N), RawText='Settings',
     SourceTier=Primary, ObservationSequence=seq, FrameId=frame)
   ✓ 投影正常（所有元素都被投影）

③ SettingsSemanticCapability.InterpretAsync(context)
   → Pattern 1: text.Equals("Settings", OrdinalIgnoreCase)
   → text = string.Join(" ", [RawText, RawContentDescription]).Trim() = "Settings"
   → **应该匹配** → produce Container + NonInteractive evidence
   ✓ 模式存在且测试通过

④ SemanticCapabilityRuntime.EvaluateAsync(context, current, sources, now, ct)
   → SemanticEvidenceV2Admission.Admit(evidence, admissionContext)
   → 检查链: ProtocolVersion → Manifest → Meaning → EvidenceKind →
     Observation equality → FrameId match → Tier permission →
     Facts correlation → Source metadata → Time validity
   ⚠️ 任何一步失败 → 静默拒绝 → Unknown

⑤ SemanticCapabilityEnvironment.ObserveAsync()
   → line 68-71: catch { return raw with { Evidence = Empty }; }
   → **如果 capability 或 projector 抛出任何异常，整个 observation 的证据为空**
   ⚠️⚠️⚠️ 这是最可能的 FDP：静默 catch 掩盖了真实失败原因
```

### 关键排除

| 排除项 | 证据 |
|---|---|
| Pattern 1 不存在？ | ✗ 存在（line 65-75），测试通过 |
| text 不匹配？ | ✗ 'Settings' 精确匹配 OrdinalIgnoreCase |
| 元素不在 Elements？ | ✗ frames dump 确认 seq 2 有 text_block 'Settings' |
| Elements 顺序被 Stabilize 改变？ | ✗ Stabilize 保持顺序和数量 |
| Sources 不正确？ | 可能 — 需要真机日志确认 |
| Admission 拒绝？ | 可能 — RejectionReasons 被丢弃，无诊断输出 |

### First Divergence Point（row_001）

**SemanticCapabilityEnvironment.ObserveAsync() 的 catch 子句（line 68-71）**。

这是一个**静默失败点**：如果 capability 或 projector 或 admission 在处理任何元素时抛出异常，整个 observation 的证据被设为 Empty —— 包括已经正确计算的 'Settings' NonInteractive 证据。**无日志、无诊断、无 trace**。

**具体机制**：capability 的 InterpretAsync 迭代所有 primary fact groups。如果某个**后面的元素**（不是 'Settings'）触发了异常，整个 foreach 中断，runtime 的 catch 触发，Empty 返回。'Settings' 在前面已计算的 evidence 也被丢弃。

### Owner Symbol

`SemanticCapabilityEnvironment.ObserveAsync()` 的 `catch { return raw with { Evidence = Empty }; }`

### Failed Predicate

无法确定具体 predicate（因为静默 catch）。候选：
- `SemanticObservationFactProjector.Project()` 内的 `throw new InvalidOperationException("A single correlated primary source is required.")` (line 22-23)
- `SemanticCapabilityRuntime.EvaluateAsync()` 内 capability `InterpretAsync` 的任何异常
- `SemanticEvidenceV2Admission.Admit()` 内的某个 reject 路径

### GapKind

**INSUFFICIENT_EVIDENCE (diagnostic)** — 我们没有足够的诊断证据来确定具体失败原因。静默 catch 阻止了诊断。

不是 IMPLEMENTATION_BUG（模式存在且测试通过）。
不是 CAPABILITY_COVERAGE_GAP（Pattern 1 覆盖了 'Settings'）。
是**诊断缺失**——需要先加观测点才能分类。

## 3. row_012 'Dark theme...' Evidence Chain

### 确认实际角色

从 frames 数据：
- 在 seq 4/5/10/11 以 text_block 出现
- y 位置在 'Display size and text' menu_item 行下方（紧贴）
- 文本内容是描述性文字（"Dark theme, font size, brightness"）
- **从未以 menu_item 出现** — 它不是菜单行

**判定**：stable descriptive/caption text（副标题），不是截断行（每次都完整出现，bounds 稳定）。

### 全链路追踪

```
① Vision: text_block 'Dark theme, font size...'
② Projector: Primary fact, RawText='Dark theme, font size...'
③ Capability:
   Pattern 1 (Settings title): text ≠ 'Settings' → NO
   Pattern 2 (Search): text ≠ 'Search' → NO
   Pattern 3 (Navigate up): text ≠ 'Navigate up' → NO
   Pattern 4 (LocalControl): IsLocalControl(facts)? → facts 无 switch/toggle → NO
   Pattern 5 (DuplicatePrimaryRowRendering): 同文本同位的 menu_item peer？
     → seq 10/11: 'Display' text_block (row_011) 在附近，但文本不同
     → 没有同文本的 primary peer → NO
   Pattern 6 (Preference Row): LooksLikePreferenceRow(facts)?
     → facts 无 structured clickable corroboration（'Dark theme...' 不在
       structured tier 的 clickable 行中）
     → corroboration.Any(IsNavigationRowShape)?
     → Correlate() 查找 auxiliary 匹配：
       a) RawText 精确匹配 auxiliary 的 RawText → structured tier 有
          'Dark theme...' 文本吗？→ 待确认（可能不在 clickable 行中）
       b) bounds overlap 匹配 → 如果 structured tier 有一个 non-clickable
          text 子元素的 bounds 与 vision text_block 重叠 → 可能匹配
     → 如果匹配到 IsNavigationRowShape 的 auxiliary（clickable LinearLayout）→
       会错误分类为 NavigationCandidate
     → 如果匹配到 non-clickable text → IsNavigationRowShape = false → NO
   **结果：所有 6 个 pattern 都不匹配 → 无 evidence → Unknown**
```

### First Divergence Point（row_012）

**SettingsSemanticCapability.InterpretAsync() 没有副标题/描述文字的 admission pattern**。

capability 的 6 个 pattern 覆盖：页面标题、搜索栏、返回按钮、toggle、重复行、preference row。**没有 pattern 覆盖"行下方的说明文字"**。

### Owner Symbol

`SettingsSemanticCapability.InterpretAsync()` — 缺少 subtitle/description pattern

### Failed Predicate

不适用——不是某个 predicate 失败，而是**没有任何 predicate 尝试分类副标题**。

### GapKind

**CAPABILITY_COVERAGE_GAP** — capability 的 admission pattern 没有覆盖"subtitle/description text"这个 role。现有 vocabulary 的 `ElementAffordanceKind.NonInteractive` 可以表达这个分类，但缺少触发它的 pattern。

## 4. SettingsSemanticCapability Admission Pattern Matrix

| # | Pattern | 输入要求 | 视觉要求 | 结构化要求 | 输出 | 覆盖 row_001 | 覆盖 row_012 |
|---|---|---|---|---|---|---|---|
| 1 | Container title | text == "Settings" | Primary fact 有 RawText | 无 | settings.container + NonInteractive | **✓ 匹配**（但被静默 catch 丢弃？）| ✗ 不匹配 |
| 2 | Search role | text=="Search" OR search hint OR search bar corroboration | Primary text | search_action_bar token | settings.search-role | ✗ | ✗ |
| 3 | Parent return | text=="Navigate up" OR back control corroboration | Primary text | back-control cd | settings.navigate-up (Relation) | ✗ | ✗ |
| 4 | Local control | toggle/switch shape | Primary facts 有 toggle 形状 | toggle corroboration | preference-row + LocalControl | ✗ | ✗ |
| 5 | Duplicate primary row | 同文本同位 primary peer | Primary facts 有同文本重叠 peer | 无 | preference-row + NonInteractive | ✗（无 peer）| ✗（无同文本 peer）|
| 6 | Preference row | LooksLikePreferenceRow OR nav-row corroboration | Primary row shape | clickable LinearLayout | preference-row (NavigationCandidate) | ✗ | ✗（不可点击）|

## 5. Existing Semantic Vocabulary Check

| Role | 词汇存在？ | 能表达？ |
|---|---|---|
| PageTitle → NonInteractive | `ElementAffordanceKind.NonInteractive` ✓ | ✓ Pattern 1 已有 |
| Caption/Description → NonInteractive | `ElementAffordanceKind.NonInteractive` ✓ | ✓ 词汇存在，缺 pattern |
| NavigationCandidate | ✓ | ✓ Pattern 6 已有 |
| LocalControl | ✓ | ✓ Pattern 4 已有 |

**结论**：词汇已足够表达 D 和 E 的正确分类。D 是诊断/实现问题（pattern 存在但可能被静默 catch 丢弃）；E 是 coverage gap（词汇够，pattern 缺）。

## 6. D/E Same Root Cause?

**NO — DIFFERENT_FDP**

| | D (row_001 'Settings') | E (row_012 'Dark theme...') |
|---|---|---|
| Pattern 存在？ | ✓ Pattern 1 存在 | ✗ 无 subtitle pattern |
| 测试通过？ | ✓ 单元测试通过 | 无测试 |
| 根因 | 静默 catch 丢弃证据（诊断不足）| 缺少 admission pattern（coverage gap）|
| GapKind | INSUFFICIENT_EVIDENCE (diagnostic) | CAPABILITY_COVERAGE_GAP |
| 修复路径 | 加诊断观测点 → 确定真实失败原因 | 添加 subtitle pattern |

## 7. Candidate Minimal Change

### D（诊断后修复）
在 `SemanticCapabilityEnvironment` 的 catch 子句加 trace/diagnostic：
```csharp
catch (Exception ex)
{
    _trace?.Add(new TraceEvent(runId) { Reason = $"semantic capability evaluation failed: {ex.GetType().Name}" });
    return raw with { AdmittedSemanticEvidence = AdmittedSemanticEvidenceSnapshot.Empty };
}
```
这不是行为变更——是加观测点让我们能看到被掩盖的失败原因。

### E（新 pattern）
在 capability 的 Pattern 5 和 Pattern 6 之间添加 subtitle pattern：
```
IF: Primary text_block occurrence T
AND: T 的 bounds 在某个已知 preference-row 的正下方（gap ≤ 行高的 50%）
AND: T 的文本不同于该行的文本（不是 title 的重复）
THEN: settings.preference-row + NonInteractive（副标题/描述文字）
```
纯结构（相对位置 + 文本不等），无语义、无固定坐标。

### 反例安全

| 反例 | D 的修复（诊断）| E 的修复（subtitle pattern）|
|---|---|---|
| 两个合法同文本 menu items | 不影响（只是加日志）| 不影响（要求在 preference-row 下方）|
| clickable row 中的 child description | 不影响 | 正确分类为 NonInteractive（不是 NavCandidate）|
| page title 与同名 menu item | 不影响 | 不影响（title 在行上方，subtitle 在下方）|
| local control label | 不影响 | 可能匹配 → 需要排除（检查是否有 switch corroboration）|
| first-seen partial text | 不影响 | 可能不匹配（下方无已知行）→ 保持 Unknown |
| OCR-only stray text | 不影响 | 可能不匹配（下方无行）→ 保持 Unknown |

## 8. Structured Evidence Authority Check

当前 capability 的 Pattern 6 依赖 structured corroboration（clickable LinearLayout）。Pattern 1 不依赖（纯文本匹配）。E 的修复（subtitle pattern）不依赖 structured 单独决定 —— 它要求 **primary vision 事实（text_block 存在）+ 相对位置关系（在已知行下方）**。structured 只做 corroboration（如果有的话），不做唯一判定。✓ 符合 Vision=primary 原则。

## 9. RVLM-2 Direction

原 R-VLM-2 提议"capability 将标题/副标题分类为 NonInteractive"。现在精确化为：
- D: 不是新 pattern 需求，是诊断观测点需求（Pattern 1 已存在）
- E: 是 coverage gap（subtitle pattern 缺失），词汇已够

"RVLM-2" 这个名字可以弃用 — 用 **SUBTITLE_ADMISSION_PATTERN** 更准确。

## 10. F-Class Evidence

未发现新的 F 类（截断行）证据。row_012 确认为 stable subtitle（非截断）。

## 11. Phase 2.6 Reentry Impact

修复 D（诊断）+ E（subtitle pattern）后：
- D 修复消除"静默 catch"风险（所有 capability 失败可见）
- E 修复消除 subtitle Unknown（预期消除 row_012 类阻塞）
- 剩余 Unknown 取决于 D 诊断结果（可能揭示其他被掩盖的失败）

**Reentry 在 D+E 修复并验证前保持 NOT_READY。**

## 12. Development Semantic IR

### D (row_001 'Settings')

```yaml
DesiredReality: Settings 根页标题 'Settings' 被分类为 NonInteractive
ObservedReality: 在真机 campaign 中被分类为 Unknown
ExistingEvidence: Pattern 1 存在 + 单元测试通过 + frames dump 确认元素存在
EvidenceGap: 无诊断证据（静默 catch 丢弃了失败原因）
GapKind: INSUFFICIENT_EVIDENCE (diagnostic)
FirstDivergencePoint: SemanticCapabilityEnvironment.ObserveAsync() catch clause
Owner: SemanticCapabilityEnvironment (diagnostic gap)
FailedPredicate: 未知（被静默 catch 掩盖）
CandidateMinimalChange: catch 子句加 trace 输出
ForbiddenChange: 不改 admission 逻辑（先看诊断结果）
SemanticResolution: UNRESOLVED (需要诊断数据)
```

### E (row_012 'Dark theme...')

```yaml
DesiredReality: 副标题/说明文字被分类为 NonInteractive
ObservedReality: 被分类为 Unknown（无 pattern 匹配）
ExistingEvidence: 词汇 NonInteractive 存在 + 位置关系可结构化判定
EvidenceGap: 缺少 subtitle admission pattern
GapKind: CAPABILITY_COVERAGE_GAP
FirstDivergencePoint: SettingsSemanticCapability.InterpretAsync() 无 subtitle pattern
Owner: SettingsSemanticCapability
FailedPredicate: N/A（无 pattern 尝试）
CandidateMinimalChange: 添加 subtitle pattern（结构相对位置 + 文本不等）
ForbiddenChange: 不用文本语义/固定坐标/XML 单独决定
SemanticResolution: RESOLVED (FDP + Owner + mechanism 明确)
```

## 13. Final Ruling

**RESULT C: D/E 根因不同（DIFFERENT_FDP）+ RESULT E: D 需要继续诊断**

- **E (subtitle)**: CAPABILITY_COVERAGE_GAP，推荐 MINIMAL_CAPABILITY_COVERAGE_CHANGE（添加 subtitle pattern）
- **D (page title)**: 静默 catch 掩盖真实原因，推荐 CONTINUE_DIAGNOSIS（先加诊断观测点）

**Next Human Gate: D_DIAGNOSTIC_TRACE + E_SUBTITLE_PATTERN_IMPLEMENTATION**
