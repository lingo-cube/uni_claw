# SIM-003 Step 1 — Executable Expectation Single-Source Design

> Status: DESIGN（Explore/Design only；本文档为 SIM-003 的 PLAN 输入）
> 前置：SIM-003 Step 0 审计（G2 compliance gap，verdict B）。
> 约束：不改代码、不改 JSON、不运行认证写操作、不重开 SIM-002、不动 C8 baseline。

## 0. 对指令本身的裁决（认同 + 三点修正）

方向认同：**JSON 为 authoring truth，C# 为投影，不采用双写+比对**。修正三点：

1. **方案 A 不是「不优」，是架构上不可能**。scenario→carrier 是 N:1 带 options 的关系，
   不是 1:1（见 §3 事实）。选项空间实际只剩 B / C。
2. **保留一个 tripwire 测试（derived-vs-certified digest 比对）**。它不是被否决的
   「双写+比对」——没有任何手写第二份期望需要维护；它是 M3 的执法报警器
   （防有人绕过投影重新引入字面量）。零维护成本，复用 cert-exp-v1 canonical。
3. **发现一处必须随迁移修正的数据错误**：SCN-WIFI-003 的 JSON
   `expectations.status = "AlreadyTerminal"` 描述的是该测试第三Drive幂等探针，
   不是 AcceptancePassed 契约（其 run 的 report.RunDriveStatus = "Completed"）。
   投影化后该条目会立即 RED。修正：status → "Completed"（AlreadyTerminal 事实
   已由 DeterministicScenarioTests L125 的 Type-B 断言承载，无信息损失）。

---

## 1. Root Cause

`bundle.Expected` 是手写 C# 字面量，与已认证的 `scenarios/*.json expectations`
之间不存在任何派生或比对关系——certification 有锁无消费方，executable expectation
有消费方无锁。

## 2. Recommended Architecture

```text
scenarios/SCN-*.json                       ← 唯一 authoring truth
  ├─ expectations（值）                      （已有 C8 certification 钉扎）
  └─ execution（绑定：kind / carrier / options）  ← 新增，schema 执法
        ↓
ScenarioExpectations.Load(scenarioId)      ← 新·唯一投影（~60 行，fail-closed）
  1. 读 JSON expectations → ScenarioCertification.ExpectationsSnapshot（复用，不造第三 DTO）
  2. 校验 certification 块存在 + expectationsDigest 自洽（DigestOf(snapshot) == 钉扎值）
     → 不自洽 = ScenarioBundleException（fail closed，全套 carrier 测试当场红）
  3. 字段 1:1 → ScenarioExpectation（enum 名可解析性校验）
  ※ 不校验 runtimeSourceHash（M4 的家在认证层；且逐 bundle 构造哈希 200+ 文件不可接受）
        ↓
GoldenScenarioBundles / ScenarioBuilder.FromTemplate   ← carrier authoring
  （stimuli / script / contract / goal 不变；Expected = 投影结果，字面量全部删除）
        ↓
MinimalScenarioBundle.Expected → ScenarioRunner.AcceptancePassed（不变）
        ↓
ExecutableExpectationBindingTests          ← tripwire（执法报警，非第二真源）
  对每个 execution.kind=golden-bundle 条目：
  DigestOf(snapshot(bundle.Expected)) == certification.expectationsDigest
```

漂移失去产生条件：**不存在可手改的 executable literal**；改期望只能改 JSON，
而 JSON 被 certification 钉住（改了必须搭乘致因 change 重认证）。

## 3. Scenario Binding

**事实（决定了选项）**：scenario→carrier 是 **N:1 + per-scenario options**：

| SCN 条目 | carrier（bundle 工厂） | options |
|---|---|---|
| SCN-WIFI-001 | `wifi-off-to-on` | — |
| SCN-WIFI-003 | `wifi-off-to-on`（同一 carrier 复用） | `duplicateActivation` |
| SCN-SMOKE-001 | `wifi-off-to-on`（同一 carrier 复用） | — |
| SCN-WIFI-006 | `wifi-off-to-on` 经 `ScenarioBuilder.FromTemplate` 派生 | — |
| SCN-WIFI-002/004/005、BARRIER-001..003 | 各自一一对应 | 004/BARRIER-002/003 `phased` |
| SCN-PERC-001..008 | **无 bundle carrier**（AsyncPerceptionHost 自有 harness） | — |

- **A（bundle 直接用 SCN id）拒绝**：N:1 共享 + options 无法装进 bundle id；且
  ScenarioImporter 的 `"sim:" + ScenarioId` 关联、既有 sealed trace / evidence 的
  id 链全部重写，纯破坏无收益。
- **C（独立 manifest）拒绝**：第三个文件 = 绑定关系的第二真源，跨文件一致性
  又要一套执法——违背单真源原则。
- **B（JSON 增 `execution` 块）推荐**：绑定与期望值、认证同文件；schema 可执法；
  C# 侧解析 fail-closed；非 bundle 场景用 `kind:"none"` 显式表达，不被强迫成
  GoldenBundle。

```json
"execution": {
  "kind": "golden-bundle",          // golden-bundle | none（新 kind 等真买家，ADR-0026）
  "carrier": "wifi-off-to-on",       // kind=golden-bundle 时必填；C# carrier registry 解析，解析不到 = 测试红
  "options": { "duplicateActivation": true, "phased": true }   // 可选
}
```

**刻意决定（记录，不做）**：certification digest 不扩展覆盖 `execution` 块。
理由：期望**值**链路完整由 expectationsDigest 钉住；execution 只路由不产生值，
错误路由要么行为失配（测试红）要么行为等价（无损）。不为想象中的篡改预造防线。

**消费方向**：测试经解析器取 bundle（`BundleFor("SCN-SMOKE-001")` →
carrier + options + 该条目自己的期望投影），而非继续直呼工厂；WIFI-001/003/SMOKE-001
各自投影**自己** JSON 的期望（值相同是事实，不是共享理由）。

## 4. Expectation Projection

复用 `ScenarioCertification`（不造第三 DTO）：

| JSON 字段 | ScenarioExpectation 字段 | 规则 |
|---|---|---|
| `status` | `ExpectedStatus` | 必填非空；`Enum.TryParse<RunDriveStatus>` fail-closed |
| `classification` | `ExpectedClassification` | 可 null；非 null 时 `TerminalClassification` 可解析校验 |
| `effects` | `ExpectedEffects` | int（schema 已执法） |
| `agentConsultations` | `ExpectedAgentConsultations` | int |
| `unconsumedStimuli` | `ExpectedUnconsumedStimuli` | int |
| `goalSatisfaction` | `ExpectedGoalSatisfaction` | 可 null；非 null 时 `GoalSatisfaction` 可解析校验 |

- unknown field：schema `additionalProperties:false`（coverage 层 exit 1）；C# 解析只读已知字段。
- certification.schemaVersion ≠ 1 → fail。
- 缺 certification 块 / digest 不自洽 → 拒绝投影（M1 的执行层防线）。
- canonical 完全复用 cert-exp-v1（`RenderCanonical`/`DigestOf`）——tripwire 与认证
  两侧逐字节同源。
- `ScenarioBuilder.Expect(...)` 保留：派生场景的函数变换（变换的是投影结果，
  如 Inject 测试 `ExpectedUnconsumedStimuli: 0→1`），不是第二 authoring 面。

## 5. Test Assertion Policy

**判定规则**：断言若复述 6 个期望字段之一在 report 表面的值 → Type A（必须删除）；
断言处于期望模型之外的维度（协议/结构/host 侧事实）→ Type B（保留，归测试代码
评审管辖，M6）。

**Type A（删除；AcceptancePassed + DescribeAcceptance 已承载）**，迁移清单：
- `SimulationHostSmokeTests` L20/22/23/24/27（"Completed"/Completion/1/2/Satisfied 全部复述）。
- `DeterministicScenarioTests`：S1 L37/40/41/43/61；S2 L85/87/88/89/90；S4 L149/153/159；
  S5 L210/211/219（status/effects/consults/goal 复述）。
- `TwoStepBarrierTests` L33/34/35/38/39/41（同上类复述）。

**Type B（保留，逐类理由）**：
- decision-id 协议格式（`decision-{RunId[^12..]}-1`）——run 关联协议不变量；
- stimulus 消费**顺序/身份**、UnexpectedStimuli 身份——6 字段模型只约束计数，
  顺序与身份是结构事实；
- `Reason` 字符串（"post-action-evidence"/"no-response"）——不在期望模型内；
- host 侧计数与终态（`host.Facts.IsRunTerminal`/`EffectReceipts`/`EffectDeliveryCount`）——
  owner 投影的架构事实，独立于 report 面；
- SCN-WIFI-003 的第三 Drive `AlreadyTerminal` 幂等探针——协议不变量（迁移后
  JSON 不再承载它）；
- `ModelCalls "N/A"`、AgentViolations 为空——metrics/纪律不变量。

**第三类（明确豁免）**：`FailClosedScenarioTests` 的内联
`new ScenarioExpectation("AgentDecisionFailed", …)` 是 test-local 故障注入 fixture
（自有本地 ScenarioId，无 SCN 条目、无认证）——不属于库真源，允许直接构造。
`ScenarioImporter` 的 `with` 变换同理（派生重跑语义）。

## 6. CRLF Strategy

**唯一推荐：方案 C = A（correctness）+ B（hygiene），A 为必须，B 为附加防线。**

- **A（哈希算法内归一化）= correctness mechanism**：`scenario_certify.py
  runtime_source_hash()` 与 `ScenarioCertification.RuntimeSourceHash()` 在哈希前对
  文本源（*.cs/*.csproj）统一 CRLF→LF。先例：`BundleAssetFiles.HashBytes` 已对
  .json/.md 归一化并注释了此坑——源哈希层补齐同一处理。归一化后哈希与
  checkout 配置正交，任何机器/任何 autocrlf 复算同值。
- **B（.gitattributes `*.cs/*.json/*.py text eol=lf`）= repository hygiene**：
  消除无谓 diff 噪声与编辑器翻转，作为第二道防线；**明确不是哈希正确性的前提**
  （autocrlf 是机器本地配置，仓库不得依赖它保证执法可复现）。

## 7. Migration Impact（列出，不改）

| 文件 | 改动 |
|---|---|
| `scenarios/schema.json` | +`execution` 块定义；迁移完成后入 required |
| `scenarios/SCN-*.json` ×18 | +`execution`（10 条 golden-bundle；PERC×8 + 其余按实况 `none`）；**SCN-WIFI-003 status: AlreadyTerminal→Completed（数据修正）** |
| `tools/scenario_certify.py` | `runtime_source_hash` CRLF→LF 归一化 |
| `tests/.../ScenarioCertification.cs` | `RuntimeSourceHash` 同归一化（与 python 逐字节一致）；+投影 mapper |
| `tests/.../GoldenScenarioBundles.cs` | 5 处 Expected 字面量 → `ScenarioExpectations.Load(...)` 投影 |
| `tests/.../DeterministicScenarioTests.cs`、`SimulationHostSmokeTests.cs`、`TwoStepBarrierTests.cs` | 删 Type-A 断言；WIFI-003/SMOKE 改经解析器取 bundle |
| 新 `tests/.../ExecutableExpectationBindingTests.cs` | tripwire + M1–M6 证明（temp 副本模式，同 ScenarioCertificationTests） |
| 认证 | 全库重认证 `--change SIM-003`（expectationsDigest 仅 WIFI-003 变；runtimeSourceHash 因归一化全体变）——合法搭乘：SIM-003 本身就是致因 change |

不改动：`ScenarioRunner` / `SimContract` / `ScenarioBuilder` / `SimulationHost` /
C8 baseline / SIM-002 任何记录。

## 8. Acceptance Tests（M1–M6 机械证明）

| Mutation | 必须失败/通过的机制 | 证明测试 |
|---|---|---|
| **M1** 改 JSON 期望不重认证 | ①投影门：digest 自洽校验失败 → ScenarioBundleException → 全 carrier 测试红；②认证测试红；③certify --check / coverage exit 1 | `TamperedJson_RefusedAtProjection_AndFailsCertification`（temp 副本改 effects） |
| **M2** 改 JSON 期望 + 合法重认证 | 投影自动流入 bundle.Expected，零 C# 手改 | `RecertifiedExpectation_FlowsIntoBundle`（temp 场景目录：改值+重盖 digest → Load 返回新值） |
| **M3** 只改 executable expectation | 字面量已不存在；若重新引入/绕过投影 → tripwire：`DigestOf(snapshot(bundle.Expected)) ≠ certification.expectationsDigest` → 红 | `EveryGoldenBundle_ExpectationMatchesCertifiedDigest`（全库遍历） |
| **M4** 改 Product Runtime 源 | runtimeSourceHash mismatch → 认证测试 + certify --check + coverage 红 → 强制 `--change` 重认证 | 既有 `StaleRuntimeSourceHash_FailsCertification`（机制不变） |
| **M5** LF↔CRLF checkout | 归一化后两侧哈希同值 | `RuntimeSourceHash_IsCheckoutInvariant`（同内容 CRLF/LF 两份 temp 源集，哈希相等）；python 侧以 fixture 双版本跑 --check |
| **M6** 改 Type-B 断言 | 认证零涉及（digest 只盖 expectations；runtimeSourceHash 只盖 Kernel/Agent，不含测试源）；仅 coverage freshness 要求重跑 TRX | `TestOnlyEdit_DoesNotTouchCertification`（改测试文件后 certify --check 仍 PASS） |

## 9. Risks（真实架构风险）

1. **scenarios/*.json 成为执行载荷**：文件缺失/损坏 → 仿真套件整体红。这是有意的
   fail-closed，但意味着 bundle 测试从此依赖 repo 布局（既有 GoldenPaths 模式已如此）。
2. **6 字段模型边界靠评审守护**：tripwire 只执法字段级复述（Type A）；「某断言
   是 A 还是 B」的判定是规则不是机器——模型外维度（顺序/身份/Reason）留在代码里，
   分类规则需在 review 中执行。
3. **PERC×8 残留未绑定**：其 harness（AsyncPerceptionHost）不走 bundle/投影，
   本设计将其显式标 `kind:"none"`——certification 对它们保护的是**文档化 golden
   记录 + G3 测试状态真值链**，不保护可执行期望值。残留第二真源（JSON vs 测试内
   断言）如实记录；`execution.kind` 留了毕业通道，等其 harness 获得投影能力再迁。
4. **一次性全库重认证**：runtimeSourceHash 归一化使 18 条 stamp 全部失效重盖——
   git 历史噪声，且窗口期内 CRLF 检出上认证本就是红的（现状即如此），无回退风险。
5. **enum 名校验收紧 status 字符串**：JSON schema 对 status 仍是自由字符串；
   投影层 enum 解析把错误提前到构造期（更早、信息更清晰），合法数据零影响。

## 10. Verdict

**READY_TO_IMPLEMENT**

无缺失架构决策。三个前置事实已核实：scenario→carrier N:1 映射表（§3）、
SCN-WIFI-003 数据修正点（§0.3）、CRLF 归一化先例（`BundleAssetFiles.HashBytes`）。
实现顺序建议：CRLF 归一化（解红）→ schema/execution 块 → 投影 + 工厂改造 →
Type-A 清理 → tripwire 套件 → `--change SIM-003` 全库重认证。
