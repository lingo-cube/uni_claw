# evidence/ — 完成证据

> Durable Artifact：Evidence proves completion。Worker 自述不算完成证据。

## 收录范围

- 验证输出（测试运行记录、命令输出摘要、RED→GREEN 转变记录）。
- 评审结论（code-review 的 APPROVE/REJECT 及依据）。
- 诊断记录（最小复现、FDP 定位、根因分析）。
- 人工裁决回执（Human Gate 决定）。

## 命名

`<WI-ID>/<类别>-<序号>.md`（如 `WI-EXAMPLE-001/tdd-red-green.md`），或对
非 WorkItem 绑定的证据用 `YYYY-MM-DD-<slug>.md`。

## 规则

- 证据必须可机械核对或可复现：附命令、路径、输出摘要；大体积原始产物
  只留摘要 + 指针。
- 完成判定（Complete）只依据 acceptance + 本目录证据；不得依据执行者归属
  （Codex / DSH）。
- 不修改历史证据；更正以追加方式记录。
