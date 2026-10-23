# Evidence Packet v0 —— 诊断的标准出口（schema · 落盘 · 引用）

> 本文件是 `uniclaw-debug-evidence` 产出的**唯一权威 packet 形态**。
> 字段语义 = SKILL.md §5 七条 + DBG-001 A4 八字段，本文件不发明新语义，
> 只补三件缺失：序列化、落盘约定、下游引用方式。
>
> 边界不变：packet 是**非权威观察**，不是事实、不是立案、不是修复授权。

## 1. 何时必须落盘（分级）

| 场景 | 要求 |
|---|---|
| E0-E1 简单诊断，无下游消费 | 对话内文本即可，不强制落盘 |
| E2+ 诊断 | **必须**落盘 packet 文件 |
| 诊断结论将被任何下游消费（立案 / EVO case / 触发器 / 工单） | **必须**落盘，且下游只引用不复制 |

落盘位置：消费方目录（如 `changes/<id>/evidence/<cycle>/diagnosis.packet.json`）。
命名固定 `diagnosis.packet.json`。同一诊断被二次消费时**引用原文件 + sha256**，
不复制第二份。

## 2. Schema（uniclaw.diagnosis.packet.v0）

```json
{
  "schema": "uniclaw.diagnosis.packet.v0",
  "authority": "non-authoritative",
  "meta": {
    "diagnosedAt": "<date>",
    "producer": { "kind": "agent-skill | model-procedure", "model": "<可选，model-procedure 必填>" },
    "changeId": "<可选：诊断所属 change>",
    "runCorrelation": "<可选：RunId>"
  },
  "subject": {
    "runDir": "<可选>",
    "reproduce": { "command": "<可一条命令执行>", "trx": "<可选>" },
    "inputs": [ { "path": "<repo 相对>", "sha256": "<hex>", "role": "<capture-xml|corpus|trace|trx|…>" } ]
  },
  "evidenceLevel": "E0|E1|E2|E3|E4",
  "evidenceSummary": ["<关键证据条目，各带指针>"],
  "failureClass": "A|B|C|D|E|F",
  "lifecycleStage": "<所处生命周期阶段>",
  "lastCorrectState": "<最后已知正确状态>",
  "firstDivergence": {
    "where": "<file:line 或 seam id>",
    "expected": "<独立预期（禁 actual 回填）>",
    "observed": "<实际观测>",
    "evidenceRefs": ["<red.trx / trace span / 源码行的具体指针>"]
  },
  "owner": "<最小 owning seam>",
  "rootCauseSupport": ["<解释全部已知证据；未解释项单独列出>"],
  "remainingUncertainty": ["…"],
  "escalation": { "required": false, "reason": "…?", "decisionRequest": ["…"] }
}
```

字段与既有定义的映射（零新语义）：

| schema 字段 | 来源 |
|---|---|
| evidenceLevel / evidenceSummary | SKILL.md §1 / §5-1 |
| failureClass / lifecycleStage / lastCorrectState | §2 / §5-2 / DBG-001 A4 |
| firstDivergence | §3 / §5-3 |
| owner | §4 / §5-4 |
| rootCauseSupport | §5-5 |
| remainingUncertainty | §5-6 / A4 |
| escalation | §5-7 / §4 边界 / A4 |

## 3. 反作弊（缺一即 packet 无效）

1. `firstDivergence.expected` 不得来自 actual / 待测输出 / 模型自证。
2. `firstDivergence.evidenceRefs` 不得为空 —— FDP 必须落在具体证据上。
3. `producer` 必须如实标注；model-procedure 必填 model。
4. `authority` 恒为 `"non-authoritative"`，任何消费方不得改写。
5. packet 的存在不构成事实：立案走 Uniflow，修复走 IMPLEMENT，均另行裁决。

## 4. 下游引用方式

| 消费方 | 引用形态 |
|---|---|
| EVO case manifest（`uniclaw-evolution-loop`） | `"diagnosis": { "path": "…", "sha256": "…" }`（LOCK 后补充） |
| Uniflow bug 类 Change | `state.md` 增 `diagnosis: <path>` 行（EVO-002 接线，本版先定义） |
| 触发器 / 工单（未来） | 只消费 `meta` + `failureClass` + `subject.runDir`，不复制正文 |

## 5. 示例（真实数据：EVO-001 cycle-01）

```json
{
  "schema": "uniclaw.diagnosis.packet.v0",
  "authority": "non-authoritative",
  "meta": {
    "diagnosedAt": "2026-10-23",
    "producer": { "kind": "agent-skill" },
    "changeId": "EVO-001",
    "runCorrelation": "n/a（离线红例）"
  },
  "subject": {
    "reproduce": {
      "command": "dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore -m:1 -nr:false --filter FullyQualifiedName~RecordedDefaultAppsPage_IsNotProofOfPopupSolelyBecausePackageChanged",
      "trx": "changes/EVO-001/evidence/cycle-01/red.trx"
    },
    "inputs": [
      { "path": "evidence/runtime-plan-006/depth2/epoch-009/run-20261020-162121-900/evidence/capture-08173a863262439ca1bac49827ad4e6e.xml", "sha256": "322c8b47b2b6d6ba0453ca6dcff483edf169c71e4669197af4587edaff228599", "role": "capture-xml" },
      { "path": "src/UniClaw.Host/SettingsPopupClassifier.cs", "sha256": "78619759f6bbc0421628205bfdd06f362176425def8492c51cfa3918c3d40e69", "role": "source-baseline" }
    ]
  },
  "evidenceLevel": "E2",
  "evidenceSummary": [
    "red.trx：失败消息与 SIM-016 归档逐字一致（asserted as popup: present）",
    "录制 XML：根 package=com.android.permissioncontroller，结构为内容页（preference_fragment_container/recycler_view），无任何 dialog id",
    "对照 e1 真弹窗 XML：同包名，但含 parentPanel/alertTitle"
  ],
  "failureClass": "B",
  "lifecycleStage": "Host 观察/分类（清障授权不在此缝）",
  "lastCorrectState": "进入 Default apps 页前的上一帧分类正确（同包 absent）",
  "firstDivergence": {
    "where": "src/UniClaw.Host/SettingsPopupClassifier.cs:18（base 版）",
    "expected": "跨包正常内容页不得判 present（SIM-017 正反例表第一行：跨包只支持 package 不同，覆盖关系 unknown）",
    "observed": "windowPackage != hostPackage 直接返回 'present'，先于任何结构证据检查",
    "evidenceRefs": ["changes/EVO-001/evidence/cycle-01/red.trx", "changes/SIM-017/design.md §当前链路与真实缺口"]
  },
  "owner": "UniClaw.Host — SettingsPopupClassifier",
  "rootCauseSupport": [
    "包名启发式无法区分『跨包内容页』与『跨包覆盖窗』——两者根 package 相同（permissioncontroller）",
    "未解释项：同包覆盖窗是否只靠白名单识别（SIM-017 反向缺口，本轮无证据）"
  ],
  "remainingUncertainty": [
    "无窗口管理/覆盖关系证据来源，present/absent 的语义边界依赖 SIM-017 五项裁决",
    "live 行为未取证（未启设备/模型）"
  ],
  "escalation": { "required": true, "reason": "分类语义属 SIM-017 冻结边界", "decisionRequest": ["SIM-017 五项 Owner 裁决"] }
}
```

此示例即 schema 的首个校验样例：字段可完整表达一次真实 E2 诊断。
