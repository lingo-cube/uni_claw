# PROJECT_LEADER_STABLEKEY_CONTAINER_SCOPE_AND_IDENTITY_INHERITANCE_RESULT

> Gate：PROJECT_LEADER_STABLEKEY_CONTAINER_SCOPE_AND_IDENTITY_INHERITANCE_ARCHITECTURE_GATE。
> 分类结论：**IMPLEMENTATION BUG（身份域作用域缺失）→ 按 gate §7 授权 minimal identity-scope repair**
> （contract 无冲突；实现把候选关联键当 run 级全局源身份传播）。
> Phase 2.6 维持 STOPPED。

## 1. StableKey 规范含义（来自现有 contract/实现/测试，非本次 bug 定义）

- `docs/decisions/project-leader-runtime-debugging-toolchain-foundation-result.md`：**身份纪律**：
  `StableKey != SameOccurrence proof`、`RowId != SameSource proof`、`Bounds != Identity`、`Text != Identity`；
  StableKey/RowId = **候选关联键（candidate correlation）**，永不升级为 authority。
- `src/UniClaw.Runtime/Model/Observation/ObservedElement.cs`：StableKey = 可选感知层稳定行标识；
  **非 null 时签名构造优先使用**（`StableKey ?? Text|PerceptionType`）。
- `SourceEquivalenceNormalizer.LogicalRowKey`：StableKey 用作**同一逻辑行的行内分组键**（投影排序）。
- C# `RowIdentityContext`：known-rows 内存 "Reset per Run"（**run 级，无可变生命周期叙述**）。
- **规范等号**：StableKey 不是跨界身份证明；没有任何 contract 声称跨 Container 相同 key = 同一 source。

**第 2 节语义答案**：StableKey 在契约中是 **D 候选跨帧表示关联键（candidate correlation key）+ 行内分组键**——
恰好不是 A（物理占用身份）、不是 B（未证实=逻辑源身份proof）、也不是 E 全局源身份。实现（D2 注释
"row_id 是 stable identity, immutable for the Run"）**超出了契约的候选定位**——实现把候选键用成了
run 级全局身份（第 10 节）。

## 2. 精确 Z4 StableKey provenance（raw → row_013 赋给标题）

```
raw detection: 工具栏标题 'Accessibility'（全宽顶带, cy≈0.089）
  + 内容行 'Accessibility'（左缘, cy≈0.138）          [YOLO 目标 + OCR 文本]
→ normalized / fusion（publication engine → candidates，stabilize=True）
→ canonical occurrence（候选 'Accessibility'）
→ row stabilizer 输入（python stabilize_with_context(candidates, known_rows=X-Known-Rows, …)）
→ candidate matching（D5）：
     _normalize('Accessibility') 精确命中 known row 'Accessibility'(row_013)
     → DIRECT row_id = 'row_013'（D5 步骤1；纯文本匹配，无 band/位置/容器/epoch 参与）
→ C# RowIdentityContext.Stabilize L48-56：python-confirmed key 直接接受，
     _bandToId[band(cy 0.089→band2)] = row_013     ← 跨 band 污染（根行原 band≈16）
     内容行同样被 python 文本命中 row_013 → band4 也 = row_013
→ semantic provider menu_item（标题与内容行均 menu_item）
→ 同帧重复签名（row_013|menu_item ×2）
→ scroll stability dup ambiguity（HasDuplicateSignature）
→ 3 观测预算耗尽 → fail-closed
```

**回答门 §1 的八问**：
- row_013 最初创建：**根页（Settings root）'Accessibility' 导航行**，the run 早期（根 epochs）分配。
- 当前 assignment 查什么：**run 级 known_rows（X-Known-Rows = RowIdentityContext.ToHeaderJson，跨容器累积）**；
  匹配按文本（python D5），不查 previous viewport / not container-scoped。
- Container 参与 reconciliation：**无**（python 不知容器；C# context 无容器域）。
- epoch 参与：**无**。
- text 权重：**匹配的唯一定义者**（D5 全文本相似），身份纪律说 Text != Identity——实现让 text 直接决定 id。
- 标题 occurrence 有 SameSource 证据指向 parent row 吗：**无**（仅文本相似）。
- 为何 destination 标题能继承 source 行身份：**run 级 known_rows + 纯文本 D5 + C# 无域校验接受**。

## 3. 源/目的容器 · 4. 现实现作用域 · 5. 跨容器继承谓词

- 源容器：`SettingsRoot`（row_013 = 'Accessibility' 导航行）；目的容器：`SettingsSubpage(Accessibility)`（标题+内容行）。
- 现作用域：python `known_rows`（run 级, D4 "alive for one Run"）+ C# `RowIdentityContext`（per-Run reset）。
- 跨容器继承谓词 = **D5 纯文本命中**（_normalize 相等 → DIRECT row_id），无任何 scope 检查。

## 6. 身份域模型（候选，与门 §3 一致）

```
StableSourceIdentity := ContainerIdentity + LocalStableKey
SAME LOCAL KEY IN DIFFERENT CONTAINERS != SAME SOURCE
NEW CONTAINER ENTRY MUST NOT CARRY SOURCE IDENTITY WITHOUT EXPLICIT RECONCILIATION
```
键值不必字面改名（保留 row_NNN 全局唯一）；**已知行"提供面"按容器域隔离**：
当前帧只提供当前容器域的 known_rows；验证返回时重植父域（保持键值稳定，不破坏 parent-return/post-completeness）。

## 7. 8 反例分析（对照门 §6）

| # | 反例 | 修复后行为 |
|---|---|---|
| 1 | 同容器同行走下一视口 | 域未变 → key 保留 ✓ |
| 2 | 同容器滚动回访 | 域未变 → 既有reconcilation 保留 ✓ |
| 3 | verified parent return | 父域重植 → 父行身份恢复（键值不重编号）✓ |
| 4 | parent 行文本 == child 标题文本 | **子容器域不含父行 → 标题获得新 key（不继承 row_013）** ✓ ROW_013 修复核心 |
| 5 | 同容器同文本两行 | C# FindOrCreateId 文本+band → 不同 band 不同 key（既有行为）✓ |
| 6 | 同文本+近 bounds 但异容器 | 域隔离 → 不 merge ✓ |
| 7 | 真重复表示（同源同表示） | 既有 composition/P5 行为不变（本次不触碰判定）✓ |
| 8 | 源行→目的页同标题 | SourceIdentity(row_013@Root) 与 DestinationIdentity(新key@Child) 保持 distinct ✓ |

## 8. implementation bug vs contract gap

**IMPLEMENTATION BUG**：契约（身份纪律）已明确 StableKey/RowId = 候选关联、非 SameSource 证明；
实现（python D5 run 级 known_rows + C# RowIdentityContext L48 无域校验接受 + Run 级 reset）把候选键
当 run 级全局源身份传播——**作用域缺失**。契约无需改动。

## 9. minimal repair candidate（gate §7 授权形式）

单一接缝：`src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/RowIdentityContext.cs`
（known-rows 属主，harness 侧）：
- 增加**容器域**：`BeginContainer(identity)`（验证进入新容器时切域；verified return 时重植父域）；
- `ToHeaderJson()` 只提供**当前域** known_rows → python 不可能再跨域文本命中；
- `Stabilize` 的 python-confirmed key 仅登记进当前域；
- **不做**：PageTitle 几何启发、budget/step 改动、duplicate 放宽、text/容器名特判、normalizer/OCR/completeness。
- 副作用为零：同域滚动/回访/返回/真重复表示行为全部不变；键值不重编号（post-completeness 稳定）。

## 10. OpenSpec 需求

**不需要**（implementation bug，minimal scope repair，gate §7 已授权；无 contract/spec 变更；
不新建 abstraction/boundary）。

## 11. PageTitle 架构是否仍需

**独立登记，本轮不要求**。身份修复后：子页标题成为**全新 key 的普通候选**（可能一时为
NavigationCandidate/Unknown——门 §8 明示 identity correctness first, semantic role second）。
是否购买 PAGE_TITLE_ROLE 依修复后 fresh evidence 另行裁决（上一 SETTINGS_SUBPAGE_TITLE_ROLE gate
的架构选项 2/3 仍待 Leader）。

## 12. 下一 Human Gate / repair gate

- **待 Leader 批准**：minimal scope repair（RowIdentityContext 容器域）→ RED→GREEN（
  Z4 falsifier：跨容器同文本不再继承；同容器滚动/回访/返回/真重复 8 反例）→ 全量套件 → fresh Z5。
- Z5 报告字段：标题 key、内容行 key、两 key 是否 distinct、帧内重复签名计数=0、scroll-stability 结果、
  settle 次数、terminal/下一 blocker。
- Z5 后：若标题成 genuine Unknown → 按 fresh evidence 决定 PAGE_TITLE_ROLE buyer（独立 gate）。

## 13. Phase 2.6 状态

STOPPED（持续）。证据文档：本文件 + `PROJECT-LEADER-TITLE-ROLE-GATE-STOP-ARCHITECTURE.md`
（上一 gate 回滚）+ `PHASE26-SCROLL-CADENCE-COVERAGE-EVIDENCE.md`（structured 通道不可靠取证）。