# Slice 4 Spec review delta

做了什么：仅复审 `candidate-final.patch` 新增资源修复及 `writer-retention-contract.md` 的“回归扩展”段；未运行构建或测试。

原问题：资源 writer 的序列化/限额失败在 fault catch 外，失败后可再次写入；取消/关闭夹具也不再构成 v4 的合法 Initialize。

本次修改：将冻结、payload 序列化、限额与反序列化移入既有 accepted-write 失败屏障；测试补齐带 identity 与空 Snapshot 的合法初始记录，保留原取消、关闭及二次失败断言。

验证结果：Spec 无新增发现（`candidate-final.patch:121-138,141-168`；契约“回归扩展”：预接受取消/序号冲突不变、序列化/冻结/超限进入同一失败屏障）。原两文件 diff 与前次候选一致，未见遗漏。

新发现：无。

下一步：等待既定最终回归证据；本评审不重开 Slice 3，也不宣称父 Change（仍 open）完成。
