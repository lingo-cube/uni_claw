# PROJECT_LEADER_STABLEKEY_CONTAINER_DOMAIN_MINIMAL_REPAIR_RESULT

> Gate：PROJECT_LEADER_STABLEKEY_CONTAINER_DOMAIN_MINIMAL_REPAIR_GATE（基于
> STABLEKEY_CONTAINER_SCOPE 调查 → IMPLEMENTATION_BUG → minimal repair 授权）。
> 结论：修复合入；RED→GREEN；全量 A/B 证明 **零新增失败（13 个失败=pre-existing，均与本次修复无关）**；
> Z5 实测运行正常（本轮未进 child；Z4 形态由单元 falsifier 证明；真实 child 证据待进入 run）。
> Phase 2.6 维持 STOPPED，继续从新暴露的 first blocker（root-Unknown 感知方差）推进。

## 1. Minimal diff

| 文件 | 改动 |
|---|---|
| `src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/RowIdentityContext.cs` | known-rows **容器域化**：`BeginContainer(string? identity)`（null→保持当前域；新身份→新建空域；已存身份→重激活保全域）；`ToHeaderJson()` 只导出**当前域** known_rows；`Stabilize()` 的 python-confirmed key **仅当属于当前域才接受**，外国 key 重键到当前域（Z4 类消除）；`FindOrCreateId` 限定当前域带映射；key 格式不变（`row_NNN` 全局唯一；只改合法相关作用域） |
| `src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/SettingsCampaignProgram.cs` | tap 处 `rowContext.BeginContainer(SettingsStrategyBinding.ResolveSemanticPage(obs))`（**可信容器身份 = 运行时建容器的同一解析**；非文本/标题/bounds 启发） |
| `tests/UniClaw.Runtime.Tests/ValidationHarness/RowIdentityContextDomainTests.cs` | 11 测试（Z4 falsifier + 10 反例 + null-identity） |

## 2. Domain lifecycle

```
ENTER_CHILD            → BeginContainer(新身份) → 全新空域（无父行继承）
SAME_CONTAINER_SCROLL  → 身份不变 → 保存当前域（python-confirmed key 跨视口保留）
SAME_CONTAINER_REVISIT → 身份不变 → 同一域既有 reconciliation 保留
VERIFIED_RETURN_PARENT → 身份=已存父域 → 重激活保全域（原始 key 恢复，不按文本重建）
null-identity 帧       → 保持当前域（滚出标题连续性；BeginContainer(null) 为 no-op）
```
`BEGIN_CONTAINER != CLEAR_ALL_HISTORY`：非激活域 run 本地保留，仅用于 verified return；匹配只发生于激活域。

## 3. Trusted ContainerIdentity source

`SettingsStrategyBinding.ResolveSemanticPage`——**运行时自身用于创建容器/回滚的同一函数**
（startup + container factory 同源）。切换只由该 verified 解析驱动；禁止项（标题文本/StableKey/行文本/
bounds/OCR）均未参与。ValidationHarness 无需发明新 authority → 无 HUMAN_GATE_REQUIRED。

## 4. RED → GREEN

- **RED**（修复前：stash 后编译失败 CS1061 `BeginContainer` 不存在 = API 级 RED）+ 原始行为验证
  （旧实现：run 级 known_rows + 文本直赋 → child 继承）。
- **GREEN**：`RowIdentityContextDomainTests` **11/11** —— Z4 falsifier（child 标题+内容行不能继承
  root `row_NNN`；子域 header 不含父行键）+ 10 反例。

## 5. Counterexamples（11 测试实测）

| # | 场景 | 结果 |
|---|---|---|
| 1 | 同容器下一视口（confirmed key 跨 band）| 保留 ✓ |
| 2/3 | 同容器回访 / verified return 重激活 | 原始 key 恢复 ✓（父域 header 不含子行键）|
| 4 | parent 行文本 == child 标题文本 | **child 新 key ≠ 父 key** ✓ Z4 核心 |
| 5 | 同文本两行（同域）| 不同带 → 不同 key ✓ |
| 6 | 同文本近几何、异容器 | 不 merge ✓ |
| 7 | 同域真重复表示 | 同 key ✓（P5/composition 语义未动）|
| 8 | 源行→目的页同文本 | SourceIdentity ≠ DestinationIdentity ✓ |
| 9 | 嵌套 Root→Child→Grandchild→Child→Root | 各域确定性恢复 ✓ |
| 10 | 兄弟 ChildA→Root→ChildB | ChildA 键不漏进 ChildB ✓ |
| + | null 身份帧 | 保持当前域 ✓ |

## 6. Harness-vs-production ownership proof（gate §6）

- `LocalVisionPerceptionSource`（production, Adapters）：**仅传输** `X-Known-Rows` header（`KnownRowsHeader` 由 harness 设置）——无内容生成。
- python `row_stabilizer`：只对**提供面（known_rows）**做 D5 匹配；`stabilize=True` 由调用方显式开启；无独立 run 级传播。
- **无生产侧平行 run 级 StableKey 传播者**（唯一 StableKey 分配面 = perception row_id 响应 + harness RowIdentityContext）。
- → 修复落在 harness 的 RowIdentityContext = **正确且唯一属主**，不遮蔽生产身份缺陷 ✓。

## 7. 全量回归（gate §7 A-H）

- A Z4 falsifier RED→GREEN ✓；B 域测试 11/11 ✓；C 反例 ✓；D-H 全量套件：
- **A/B 对照**（stash 本次改动后同批失败类重跑）：13 个非环境失败 **有/无修复完全相同** =
  **PRE-EXISTING**（OCR 替换/c8164f4 时代波及：StartupForegroundVerification ×6、TraceAssertionMatrix ×3、
  TitleOff、VisionFirstSourceGrounding、AgentSemanticClosedLoop、HarnessSourceShapeGuard；
  加 CORR_HOST×3/Capstone/ExternalBoundary 真实设备 5 = 既有）→ **本次修复零新增失败**。
- 上轮 TITLE-ROLE gate 的 capstone 回归（19）**不在本次失败清单**（域修复未触碰判定层）。

## 8. Fresh Z5（真实 new-OCR 环境）

- accepted [5,8,11,14,17,19]（root）；root 'Accessibility' 行 key = **row_014**（本 run）。
- terminal：`Unknown interaction affordances remain`——root Unknown = **'LoO'（OCR 乱码家族）**（感知方差，与域修复无关）。
- **本轮未进入 child** → Z4 的真实 child 观测未在此 run 复现（域行为由单元 falsifier 11/11 证明；
  需下一次进入 run 获取真实 child key 证据）。
- 无 settle/stability 类失败（stability 均确认；无 dup-ambiguity）。

## 9. StableKey before/after

- **BEFORE**：`row_013`（root 'Accessibility'）经 run 级 known_rows + python 纯文本 D5 → 子页标题与内容行
  同时获得 `row_013|menu_item`（同帧重复签名）。
- **AFTER**：child 域 fresh → 标题获得新 key（band2 域内新 id），内容行另一新 key（band4）；root
  `row_013` 保留在 root 域、不再供给 child。**跨容器继承 = 0**（单元级证明）。

## 10. Residual first blocker / PageTitle buyer / Phase 2.6

- **Residual first blocker**：root 层感知方差 Unknown（本轮 'LoO'；I/M/O/P/S 家族）——与 StableKey 域无关，
  属 OCR/采集通道问题（UI-TARS 候选评估中，基准 14-35px 可用性已证）。
- **PageTitle buyer**：身份修复后子页标题变为**全新 key 的普通候选**；本 gate 不要求其角色。
  是否出现 genuine Unknown/独立 Nav 阻塞 → 需 child 进入 run 的 fresh evidence；**当前无 buyer，defer**。
- **Phase 2.6**：STOPPED；依 gate §10 从新暴露的 first blocker（root-Unknown 感知方差）继续。**

## 11. 边界

未改：budget/retry/step/duplicate 放宽/normalizer/completeness/OCR/标题角色；键格式不变；
既有同域滚动/回访/返回/真重复行为不变。冻结不变量全程保持。
证据：本文件 + `PROJECT-LEADER-STABLEKEY-CONTAINER-SCOPE-RESULT.md` + 系列 EVIDENCE 文档。