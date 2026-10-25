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

## 生命周期与收尾

过程证据由开发任务拥有，保留/归档/删除以
[开发测试资产收尾规范](../docs/agents/test-asset-retention.md)为单一来源。
诊断、重放、仿真和验证每次返回前完成本轮清理，不等到整个任务关闭。
仍需的最小复现、正式输入、packet及有效引用保留；无继续用途的临时或重复
过程输出当轮处置。历史内容不因归档而改写；显式退役追加去向说明，不冒称
已删文件仍可复验。任务删除前须迁移/解除外部依赖，已移交正式基线的材料独立保留。
