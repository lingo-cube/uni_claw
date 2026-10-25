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
  "signal": { "kind": "red-test|run-dir|trace|scenario", "ref": "<path-or-id>", "failureKey": "<新case来源信号的指纹，可选>" },
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
  "diagnosis": { "path": "<LOCK前已有时可选引用diagnosis.packet.json>", "sha256": "<hex>" },
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
6. **diagnosis 引用而非复制**：LOCK 前已有诊断时，case 可包含 `diagnosis`；
   LOCK 后不得补写或改写 manifest。LOCALIZE 新产出的诊断放在结果
   `packet.diagnosis`（path + sha256），指向 `uniclaw-debug-evidence` 的
   [packet v0](../../uniclaw-debug-evidence/references/evidence-packet.md)
   （path + sha256），不内联重造 FDP 字段。EVO-001 cycle-01 的内联 `fdp`
   段是该 schema 出现前的过渡形态，后续 cycle 一律引用。

封存不等于永久保存所有执行副本。每次重放/证明返回前按
[开发测试资产收尾规范](../../../../docs/agents/test-asset-retention.md)
保全case、独立Oracle、规定的RED/GREEN及有效引用，清理无继续用途的
派生产物并记录实际处置；不修改case/packet schema或封存hash。消费者
尚不支持归档读取时保留原引用材料，不以新索引掩盖悬空路径。

### 已落盘运行的有界导出

`python3 tools/evolution-case.py export --help`提供EVO-003首片：显式选择
Settings capture及独立Oracle，生成独占新目录中的case.json/case.sha256及原始输入副本。
导出子集的机械形状见`schemas/evolution-exported-case.schema.json`。
它沿用case.v0，追加`adapter=settings-popup.v1`和`inputBase=case-directory`；
不迁移或重写旧case。`verify`检查封存、输入/Oracle/载体和引用hash；`replay`
不执行manifest里的任意命令，仅使用固定公开分类器载体并产出真实TRX。
exit 0是匹配独立标签，1是组件标签不符，2是材料/环境/载体不可用；1不表示
原产品契约或实机窗口事实已被证明错误。两次结果仍须同manifestSha256。
原run删除后复制的组件输入仍可消费；可选诊断/信号保持外部path+sha引用，
须保留引用资料，不将正文复制进包。SIM-017裁决门不随导出或GREEN解除。

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
  "diagnosis": { "path": "diagnosis.packet.json", "sha256": "<hex>" },
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

`case` · `inputs` · `red` · `diagnosis` · `candidate` · `green` · `residualRisk` · `gate`

新循环从被引用的诊断包读取 FDP / owner，不复制诊断正文。cycle-01 原包
保留内联 `fdp` 的历史格式，不改归档或 manifest hash。

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
