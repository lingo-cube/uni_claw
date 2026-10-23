# Case manifest 与 Evidence Packet（v0）

> 本文件定义 `uniclaw-evolution-loop` 的两个不动点：**case 冻结什么**、
> **packet 必须证明什么**。两者都是 Harness 工件，不是产品协议。

## 1. Case manifest（`case.json`）

位置：`changes/<change-id>/cases/<case-id>.json`。
一次循环只允许一个 case；多个失败拆多个 case。

```json
{
  "schema": "uniclaw.evolution.case.v0",
  "id": "<stable-id>",
  "sourceChange": "<CHANGE-ID>",
  "designRef": "changes/<CHANGE-ID>/design.md#...",
  "signal": { "kind": "red-test|run-dir|trace|scenario", "ref": "<path-or-id>" },
  "inputs": [
    { "path": "<repo-relative>", "sha256": "<hex>", "role": "capture-xml|corpus|trace|oracle|golden" }
  ],
  "expected": {
    "claim": "<一句可判定的预期>",
    "oracle": "<独立 oracle / 需求 / 已审 golden 的出处；禁止 actual>",
    "assertion": "<测试断言或场景断言>"
  },
  "reproduce": {
    "project": "tests/<Project>/<Project>.csproj",
    "filter": "FullyQualifiedName~<TestName>",
    "command": "dotnet test <project> --filter \"<filter>\""
  },
  "owner": "<预期出错的组件/类型/方法>",
  "fdpExpectation": "<预期 First Divergence Point>",
  "diagnosis": { "path": "<diagnosis.packet.json>", "sha256": "<hex>" },
  "candidateDirection": "<一句话方向；不写实现细节>",
  "gate": {
    "required": true,
    "refs": ["<ADR / design / owner ruling>"],
    "decisions": ["<未决项>"]
  },
  "residualRisks": ["<这条 case 证明不了什么>"]
}
```

### 冻结规则

1. **路径不是 identity**：所有输入必须带内容 hash；hash 变即新 case。
2. **expected 只能来自独立 oracle**：需求、ADR、已审 golden、人工标注。
   禁止从本次 actual、Runtime 输出、模型输出、待测函数输出回填。
3. **reproduce 必须可一条命令执行**；不接受“手动点一下”。
4. **gate 必须显式**：需要 Owner 裁决 / 触及冻结语义时 `required: true`，
   并列出具体裁决项；不得用一句“待定”糊过去。
5. **residualRisks 不得留空**：一条 case 证明不了的东西必须写出来。
6. **diagnosis 引用而非复制**：`diagnosis` 字段（LOCK 后 LOCALIZE 产出时
   补充）指向 `uniclaw-debug-evidence` 的
   [packet v0](../../uniclaw-debug-evidence/references/evidence-packet.md)
   （path + sha256），不内联重造 FDP 字段。EVO-001 cycle-01 的内联 `fdp`
   段是该 schema 出现前的过渡形态，后续 cycle 一律引用。

## 2. Evidence Packet（`packet.json` + `packet.md`）

位置：`changes/<change-id>/evidence/<cycle-id>/`。
`packet.json` 是机器面；`packet.md` 是同一内容的人读摘要，**不新增结论**。

```json
{
  "schema": "uniclaw.evolution.packet.v0",
  "case": { "id": "...", "sourceChange": "...", "manifestSha256": "..." },
  "inputs": [ { "path": "...", "sha256": "...", "role": "..." } ],
  "red": {
    "command": "...", "exitCode": 1, "trx": "red.trx",
    "failure": "<断言消息/异常>", "matchedExpected": true
  },
  "fdp": {
    "level": "E2|E3|E4", "failureClass": "A-F", "firstDivergence": "...",
    "owner": "...", "evidenceRefs": ["..."]
  },
  "candidate": {
    "diff": "candidate.diff", "files": ["..."],
    "hypothesis": "<一个可反驳假设>",
    "changesExpected": false
  },
  "green": {
    "command": "...", "exitCode": 0, "trx": "green.trx",
    "sameCase": true, "repeatable": true
  },
  "residualRisk": ["..."],
  "gate": {
    "status": "none|decision-required",
    "decisionRequest": ["..."],
    "mergeAuthorized": false
  }
}
```

### 必填八段（缺一即 RED）

`case` · `inputs` · `red` · `fdp` · `candidate` · `green` · `residualRisk` · `gate`

### 反作弊

| 造假形态 | 检出方式 |
|---|---|
| expected 从 actual 回填 | `expected.oracle` 必须指向 actual 之外的材料；review 核对 |
| 红绿用的不是同一 case | `manifestSha256` + 输入 hash 必须一致 |
| 只跑绿、没跑红 | `red.trx` 缺失或 exitCode=0 → RED |
| 候选夹带无关改动 | `candidate.files` 与 `git diff --name-only` 必须一致 |
| flaky 记绿 | 连续两次同命令必须一致；不一致记 flaky，不记 green |
| 用全量绿替代定向证明 | 必须有 red/green 的定向 TRX；全量只作回归补充 |

### Gate 语义

- `gate.status = none`：候选可在 Uniflow 内进入 REVIEW/VERIFY；仍不得
  自动合并。
- `gate.status = decision-required`：Uniflow 最多到 `reviewed`；packet 的
  `decisionRequest` 交人；人裁决后才允许下一轮。
- `mergeAuthorized` 永远为 `false`（本 Skill 不授权合并）。

## 3. 最小示例（tracer）

见 `changes/EVO-001/cases/default-apps.json` 与
`references/tracer-default-apps.md`。
