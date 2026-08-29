## Context

Authority baselines: Runtime Architecture Contract I-1..I-14（不受本 change 影响）；
`platforms/perception` 治理体系（config-manifests → deployments → CURRENT-ACTIVE receipt，
内容哈希锚定）；`perception-navigation-row-composition-repair`（候选算子 `row_grouping.py`
四锚点语义 + v1n 三锚点误判证据）；Phase 2.6 `runtime-iterative-full-traversal-acceptance`
的 STOP 报告（IR-G0）与已验证的知识资产生命周期（ScenarioKnowledgeFixture：provenance
准入、fresh-wins、scope 隔离、版本化 freeze/load，35/35 测试）。

Human direction (2026-08-27)：不再继续放宽纯间距规则；要求泛化框架 —— 通用算子、
五维+tag 的 CSS 式树状级联规则、算子参数按维度组取值并可增量学习；并固定权威路线
（视觉行组合/关系头生成；确定性间距算子验证约束 fail-closed；文本语义只冲突检查/
降置信；VLM 仅离线标注或低频 advisory；XML 仅辅助证据）。

## Goals / Non-Goals

**Goals**

- 一个可容纳当前与未来感知组合问题的算子模型（authority-classed、参数化、确定性、fail-closed）。
- CSS 式规则树：五维 + tag 补充，树状管理，层层覆盖，解析确定且可解释（provenance 链）。
- 配置进入既有 governance 链；学习参数只在验证侧积累，晋升经 receipt 人工批准。
- 用该框架给出 IR-G0 的分片解锁路径（S1–S5），S1 与现有候选零行为差异。

**Non-Goals**

- 不改 Runtime 归一化 / SourceIdentity / Strategy Contract / GoalEvidence。
- 不让 XML、文本语义、VLM 成为菜单身份或动作授权来源。
- 不做自动生产参数推送；不做通用 UI 理解（算子只做声明的组合语义）。
- 不在本提案内实现模型版 relation-head（S3 独立 gate）。

## Decisions

### D1 — 算子是唯一的感知组合扩展点，authority class 三分类

`GENERATOR`（可产出导航/行身份候选）、`VALIDATOR`（只能确认/否决/降置信）、
`ADVISOR`（只能标注/建议，离线或低频，永不进授权路径）。菜单身份只能由 GENERATOR
（视觉行组合/关系头）产生，且必须通过全部 VALIDATOR。这是 Human 路线 1–5 的规范化。

### D2 — 算子实现通用，场景差异全部下沉为规则树参数

算子 = `(inputs, resolvedParams) → typed evidence` 的确定性纯函数；参数有界（min/max/enum），
默认值在根节点。`row_grouping.py` 的四锚点、cadence 推断、bracket/continuation、50% 上限
全部参数化 —— v1n 被回退的三锚点放宽不再需要代码分支：它是（被证据 CONTRADICTED 的）
一组参数提案，留存在学习存储里作为禁止区域记录。

### D3 — 规则匹配 = specificity 级联；树 = 组织视图（Human 澄清 2026-08-27）

- **语义层（CSS 正统）**：规则 = `pins`（五维+tags 的**任意子集**，不需要前缀/树路径）+
  `params`（按算子命名空间的参数覆盖）。specificity = pin 住的维度数（tags 每项计 1）。
  生效值 = 最高 specificity 匹配规则；匹配 = 每个 pin 维与上下文值精确相等（值或 `default`；
  tags 为子集匹配：规则 tags ⊆ 上下文 tags）。**同特异性冲突 = 载入期校验错误**
  （fail-closed，要求显式交集规则，如 `system+systemVersion+app`），**规则文件顺序不影响语义**
  （无 source-order 依赖，diff/merge/review 友好）。
- **组织层（树状管理）**：规则按维度序分层组织为目录树视图（`android/…/api-35/…/
  com.android.settings/…`），纯呈现与归档，不参与匹配语义 —— "树状缺失只是视图，具体规则靠
  组合与级联"。
- **典型级联**（Human 场景）：全 android 生效（pin system）→ 某版本异化（+systemVersion，
  覆盖）→ 某应用特值（system+app，不需 pin 版本）→ 无版本应用（上下文 appVersion 缺失取
  `default`，规则可显式 pin `appVersion=default` 匹配"无版本"情形）→ 特殊模式（tags，如
  `display=triple-screen` 三连屏时覆盖）。
- **维度 canonical 表示**：`system`（`android`…开放枚举）；`systemVersion`（`api-35` 型）；
  `app`（包名）；`appVersion`（版本串，**缺失 = `default`**）；`device`（型号如 `Pixel 7`；
  serial 归 tags：`serial=emulator-5554`）；`tags`（`key=value` 有限集：`display`、`locale`、
  `density`、`model`、`serial`、`scenario`…）。上下文值由调用方（Adapter 层）随分析请求附带，
  感知服务只消费；缺失维 = `default`（fail-safe：pin 了非 default 值的规则不适用）。
- 解析输出 `ResolvedParams{operatorId, values, provenance: value → ruleId+pins+specificity}`
  —— 确定性、可解释、可离线重放（帧 + 规则集哈希 → 唯一结果）。

### D4 — 规则树是治理化工件，运行时只消费激活配置

规则树序列化为人类可读、可 diff、确定性的 JSON（键序稳定，无时间戳/路径）；内容哈希进入
既有 `configId → deploymentId → CURRENT-ACTIVE receipt` 链。感知服务启动时加载 receipt
指向的规则树；未激活的候选树不进运行时。零新权威机制。

### D5 — 学习 = 验证侧参数知识，晋升 = governance receipt（人工）

learned-parameter 记录：`{selectorNodePath, operatorId, parameter, value, evidenceRefs,
sourceCampaignRun, status(ACTIVE/STALE/CONTRADICTED/SUPERSEDED/INVALIDATED), version,
supersedes/supersededBy, validityAssumptions}`。准入 provenance-gated（必须引用真实
campaign/离线评测证据）；冲突 fresh-evidence-wins（旧值降级，永不强制套用）；freeze/load
版本化。晋升：候选树 → 新 config manifest → deployment → receipt 切换（人工批准）。
学习永不直接改生产 —— 运行时 AuthorityDelta 恒为 NONE。该生命周期直接复用 Phase 2.6
已毕业的 ScenarioKnowledgeFixture 纪律（同一模式从场景知识推广到算子参数）。

### D6 — IR-G0 解锁分片与回退边界（Human 批次裁决 2026-08-27：S1 → S2 → S4 硬 Gate）

- **S1**：框架核心 + `uniform-list-row-grouping`（根参数 = 现候选值）+ `spacing-verifier`。
  验收：与现候选**零行为差异**（同一测试集全绿 + 帧级等价对照）——**任何差异即 STOP**。
- **S2**：确定性 `row-relation-head`，**输入冻结**为原始视觉区域（未组合检测框 + OCR 文本块）
  与成对几何关系候选（同列/垂直邻接/包含/重叠）——不得消费已成立的行组（禁止"先识别行才能
  识别行"循环）；文本/XML/VLM 不得补造行身份。输出为行组提议（含 head/satellite 选举），
  必须过 `spacing-verifier`；歧义 fail-closed。验收：v1n 误判帧不再误判且证据不足处保持
  fail-closed；四锚点行为不回退；跨 UI 回归集通过。**不足即 STOP 于 fail-closed 边界，
  不自动进入 S3**。
- **S3**（独立 Human Gate，未授权）：模型版 relation-head（新模型/部署契约、延迟预算、
  provenance、不可用行为、跨 UI 证伪集）。
- **S4**：`text-relation-check` + `structured-corroboration` 接线（仅 veto/降置信，
  不得补造候选）。
- **S5**（延后，S2 后单独决策，不阻塞 Phase 2.6 重入）：学习闭环；最小样本量、证据区间、
  提案生产者均为延后设计输入。
- **Phase 2.6 重入**：S2 或获授权的 S3 在回归帧集上稳定产出"每视觉行恰一个导航候选"后，
  重跑 Stage A→B→C→J→K。

### D7 — 停止条件

任何阶段若出现：需要修改 Runtime 归一化契约；需要 XML/文本/VLM 成为身份或授权来源；
框架无法表达某个必要组合语义；学习需要绕过 governance 直改生产 —— 立即
`STOPPED_AT_RUNTIME_OR_CONTRACT_GAP`，带 FDP 证据返回 Human Gate。

## Authority proof

| Forbidden edge | Why impossible | Guard/proof |
|---|---|---|
| 学习参数直改生产 | 学习只在验证侧存储；晋升唯一通道 = receipt 切换（人工） | governance receipt 校验 + 学习存储无生产写路径 |
| 文本/XML/VLM 生成菜单 | authority class 契约：只有 GENERATOR 可产出身份；text/xml/vlm 均非 GENERATOR | 算子注册表类型检查 + 源级 guard |
| 绕过 VALIDATOR 的生成行 | 所有 GENERATOR 输出必须通过 spacing-verifier 等 VALIDATOR | 组合管线不变式测试 |
| 同特异性规则冲突 | 选择器交集分析 + 确定性冲突检测：仅当两条同分规则的选择器存在可达交集（存在同时匹配两者的上下文）、交集上该参数未被更高特异性规则覆盖时才拒绝；互斥规则（某维 pin 了不同值）不是冲突。检测允许保守近似（无法证明交集为空或已被覆盖 → 拒绝，fail-closed 方向） | 载入期冲突检测器 + 解析器确定性属性测试 |
| 配置漂移 | 规则树哈希入 receipt；未激活树不进运行时 | 既有 governance 测试族扩展 |

## Risks / Trade-offs

- [框架过度设计] → S1 强制零行为差异 + 每片独立验收；算子数量按需增长，不预建空壳。
- [relation-head 仍不安全] → S2 验收含 v1n 误判帧回归集；不达标即停在 fail-closed 边界（等 S3）。
- [学习存储漂移为影子生产] → 生命周期完全复用知识纪律；晋升演练在验证环境完成。
- [规则树爆炸] → 最初只有根节点；叶子只在证据存在时创建（准入门槛即数量门槛）。

## Design Docs

| Concern | Doc |
|---|---|
| IR-G0 与 STOP 证据 | `../runtime-iterative-full-traversal-acceptance/evidence/STOP-runtime-or-contract-gap.md` |
| 候选算子与误判证据 | `../perception-navigation-row-composition-repair/evidence/` |
| 保留算子实现 | `platforms/perception/uniclaw_perception/fusion/row_grouping.py` |
| 知识生命周期先例 | `../runtime-iterative-full-traversal-acceptance/`（ScenarioKnowledgeFixture，35/35） |
| Normative behavior | `specs/perception-operator-rule-framework/spec.md` |
