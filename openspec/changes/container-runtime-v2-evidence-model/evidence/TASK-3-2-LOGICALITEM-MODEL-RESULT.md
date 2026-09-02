# Stage C1 Task 3.2 — LogicalItem Model 结果

> Change: `container-runtime-v2-evidence-model`
> Task: `3.2`（Leader Gate 授权；Stage 不并行；完成后停 Leader Gate）
> 日期: 2026-09（apply 会话）
> 结论: **3.2 完成，停在 Leader Gate**（3.3 未开始；2.6/2.7/2.8 保持未完成）

## 1. 修改文件

| 文件 | 变更 |
|---|---|
| `src/UniClaw.Runtime/Model/LogicalItem.cs` | NEW：`LogicalItemRef` / `LogicalStructure` / `LogicalAffordanceKind`（无 None 成员——缺位=null，与 Unknown 不可混淆）/ `LogicalMemberRole` / `LogicalItemState`（tri-state bool + value/unit/mode）/ `LogicalMembership`（显式 evidence 强制）/ `LogicalItem`（fail-closed 构造）/ `LogicalItemIntegrity`（纯静态引用完整性校验，非提交路径） |
| `tests/UniClaw.Runtime.Tests/Unit/LogicalItemModelTests.cs` | NEW：11 项测试（3 buyer + 8 机械） |
| `tasks.md` | 勾选 3.2 |

## 2. 字段语义与 authority 隔离

- **组合模型**：Structure × 单可选 PrimaryAffordance × membership roles × State；enum 全集 = V1 CANDIDATE（NOT CONTRACT-FROZEN），注释声明。
- **单主 affordance**：属性类型 `LogicalAffordanceKind?` —— 结构上至多一个；无 None 枚举成员，"无 action 语义"（null）与"未解析"（Unknown）不可混淆，杜绝"缺失 affordance → 默认非交互对象"的隐性推断（测试锁定）。
- **STATIC_CONTENT 无 action affordance 可成立**：`Structure=StaticContent, PrimaryAffordance=null, SemanticResolved=true` 合法（buyer 测试）；结构化 authority 隔离测试（反射）证明该记录**不存在** grounding 几何（无 ElementBounds 属性）、无 Groundable/Authorization/Obligation/Coverage/Completion 命名成员。
- **SemanticResolved 语义边界**：仅表示 canonical semantic claim 满足证据策略的模型级良构条件（structure ≠ Unknown ∧ affordance determined ∧ ≥1 显式 evidence-backed membership）；注释显式声明 ≠ CurrentlyGroundable/Authorized/ObligationSatisfied/CoverageExhausted/ContainerComplete。
- **membership 显式 evidence**：`LogicalMembership.EvidenceRef` 必填（空/空白 → `LogicalItem` 构造抛异常）；模型**不存在文本维度**（反射测试），文本/目的地/相邻/共同容器在结构上无法驱动合并 —— 推断属 3.3 claim-specific policy，且必须经此显式 evidence 通道记录。
- **anchor SliceRefs**：不可变、去重、经 `LogicalItemIntegrity.ReferencesResolve` 对 NodeLocalModel active∪archived 层校验（archived 锚 = 保留的 relocation 证据）；不存 live handle、当前坐标、历史 grounding 结果、可变集合（反射：无 setter、全部 ImmutableArray）。
- **Unknown 不被强制**：Structure=Unknown 保持 Unknown 且不得带 resolved claim（fail-closed 抛异常）；Unknown → STATIC_CONTENT、null → 非交互 的默认推断被结构性排除（测试锁定）。

## 3. 三 buyer 证明

| Buyer | 测试 | 证明 |
|---|---|---|
| 孪生文本 | `TwinTextEntitiesStaySeparateItemsWithExplicitEvidence` + `MembershipWithoutExplicitEvidenceIsRejected` | 相同文本的两个视觉实体保持两个 item；模型无文本维度，文本相等结构上不可合并；membership 无显式 evidence 即拒绝 |
| 帧级翻转 | `FrameLevelClassificationFlipCannotSilentlyRewriteAnItem` + 不可变测试 | 帧 A（ListItem/Navigate/Primary）与帧 B（StaticContent/null/Secondary）只能各自成记录；原记录 identity/anchors/evidence 原样保留（无 mutation API，属性无 setter）；裁决权留给 3.3 reconciler |
| STATIC_CONTENT | `StaticContentResolvesWithNoActionAffordance` + `ResolvedStaticContentCarriesNoAuthoritySurfaces` | 无 action affordance 的静态内容可 resolved；反射证明零 grounding/action/obligation/coverage/completion 权威面 |

## 4. NET_NEW_MUTABLE_TRUTH / owner

**+0**。LogicalItem 为纯不可变模型，**无 producer、无 commit seam、无第二 owner、无 identity registry、无 reconciler/merge/split/reclassification 逻辑、无双写路径**（3.3 才创建 LogicalItem 并接线 CanonicalProjection）。CanonicalProjection 骨架未动。未实施任何跨 Slice correlation 决策（2.6 保持未完成）。本轮不存在提交 seam，故 reducer rejection 测试不适用（fail-closed 由构造异常承担，已记录）。

## 5. Guard 交互

- ModelImmutabilityTests：无新白名单需求（无 Bounds/Parent/Children 等禁用字段名；`LogicalItemState.Value` 不在禁用清单）。未放宽任何断言。
- ArchitectureGuard 全系通过（见 §6）。

## 6. 验证结果

1. 3.2 聚焦：**11/11 PASS**。
2. 聚焦门（B1+B2+3.1+3.2 + ModelImmutability + R8 live-state + ArchitectureGuard 全系）：**161/161 PASS**。
3. `dotnet build` 0 error；`openspec validate --strict` PASS；`check-consistency.sh` **ALL PASS**。
4. 完整集：Runtime **2683 通过 / 4 失败**（+11 = 本轮新增通过；失败集合与 3.1 归档分类逐一相同：在途工作 2 + 真机/模拟器环境 2）；Semantic **153/5**（与基线完全相同）。无新增失败；不以聚焦通过或失败数不变宣称毕业。

## 7. Leader Gate（停止点）

3.2 完成。**3.3（EvidencePolicy + SemanticReconciler）未开始**；2.6/2.7/2.8 保持未完成（依赖裁决不变）。等待 Leader 检查后授权 3.3。

---

## 附录 · 3.2 审核修订（PASS_WITH_FIXES，2026-09；修后同轮验证）

审核裁决 `TASK_3_2_ARCHITECTURE: PASS_WITH_FIXES`。三项必修全部接受（无反驳项；第 1 项确认为模型语义错误而非风格问题），3.3 锁定项已写入 tasks.md 3.3 条目。

### 立即修复（3 项）

| # | 问题 | 修复 | 测试锁定 |
|---|---|---|---|
| 1 | `AffordanceDetermined` 的 Structure switch 把 "actionable Structure 必须有 action affordance" 偷渡进基础模型（`LIST_ITEM ≈ ACTIONABLE`），破坏 Structure ⊥ Affordance 正交；Android 版本行（ListItem+null+resolved）反例成立 | 判定只看 affordance 值：`null=determined NONE / 定值=determined / Unknown=unresolved`；**不添加任何反向 compatibility 规则**（StaticContent 必须 null / Button 必须 Invoke 等留给 3.3 EvidencePolicy） | `StructureAndAffordanceAreOrthogonalAtTheBaseModel`（ListItem+null+resolved 合法；StaticContent+Navigate 可表达不入 policy） |
| 2 | `LogicalItemRef` 等 record struct 的 `default(T)` 绕过构造器；`ThrowIfNull(struct)` 无效 | LogicalItem ctor 显式 `IsNullOrWhiteSpace(itemRef.Value)`；membership OccurrenceRef 同检；同类漏洞顺带修复 B2 `Occurrence` ctor（审核点名排查） | `DefaultStructReferencesCannotBypassConstructors`（default ref 三处消费点拒绝） |
| 3 | ref 注释 "Run-local" 超出已购范围 | → "scoped to the owning NodeLocalModel lifecycle; no cross-node/cross-run identity" | — |

另接受两处注释语义修正：`LogicalItemState` "evidence" → "canonical logical state PROJECTION"（Evidence != Projection）；`AnchorSliceRefs` 注释硬化（≠ currently visible / groundable / action-bounds source）。

### 断言修正声明（非隐藏失败）

原 `ResolvedClaimWithoutDeterminedSemanticsFailsClosed` 中 "ListItem+null+resolved → 拒绝" 子断言与 `UnknownIsNeverCoerced` 中 `IsAffordanceDetermined=false` 断言**编码了被本轮审核裁决废除的耦合**，按裁决替换为正交性正向断言（见修复 #1 测试）；裁决因果链以本附录留档。

### 3.3 锁定项（已写入 tasks.md）

① EvidenceRef 可追溯到 deterministic reconciliation decision（优先 typed ref）；② `SemanticResolved=true` 仅经 Reconciler/EvidencePolicy producer seam 产生；③ CanonicalProjection 级 occurrence membership 唯一性（0..1）；④ reconcile 幂等不依赖 ImmutableArray record 默认 equality（same evidence → twice → NO_CHANGE）。

### 修后验证

3.2 聚焦 **13/13**；聚焦门 **163/163**；build 0 error；`openspec validate --strict` PASS；`check-consistency.sh` ALL PASS；完整集 Runtime **2685/4**、Semantic 153/5 —— 9 个失败与 3.1 归档分类逐一相同，无新增。
