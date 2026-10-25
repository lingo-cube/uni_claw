# Delta Spec review — slice3 candidate-v4

范围仅为 `candidate-v4/delta-from-v3.patch`；其余结论继承 v3，未执行测试。

## Result: no new Spec defect

`IsVersioned` 现在仅在首行 JSON 根为 Object、存在 `schema`、且 schema 为 String 并等于 v4 标识时进入 v4 reader（`delta-from-v3.patch:4-10`）。因此 `{}`、`[]`、`{"schema":17}` 回落到原 legacy `Load`，由公开 `Inspect` 的既有异常转换路径返回 `AccountingComplete=false` 和 `resource-history-unreadable:*`，而非在格式探测阶段泄漏 `KeyNotFoundException`/`InvalidOperationException`。新增三例 public regression 精确锁定该行为（`:19-36`）。

补丁没有把非 v4 内容误认证为 v4，也没有改变真正 v4 首帧的 hash/head、baseline、strict replay 或 data-only version-query 校验路径；畸形 JSON 仍由 JSON parse 失败并落入同一 Inspect unreadable 结果。故该修复保持旧消费者的失败语义，符合 A3 的“损坏不发布伪最新状态”与 A9 的旧 reader 兼容边界，且未扩大为新的恢复/外源验证平台。
