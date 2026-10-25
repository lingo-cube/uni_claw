# RUNTIME-STORAGE-002 Slice 3 — Standards review v3

**Delta 结论：PASS。** 仅复核`candidate-v3/delta-from-v2.patch`。

`ReadVersionCore`现在仅当目标就是 head 声明的 latest 时，要求实际还原状态的 frame hash、last offset 和 next offset 同时等于 head 的 hash、lastOffset、length。复制末 frame 并把 head 指向复制件虽然仍可通过单帧 hash/sequence 检查，却会在还原链停于原末帧时触发`resource-latest-endpoint-mismatch`；新增物理回归准确覆盖该触发条件。普通历史版本查询不被错误收紧。

这是一项 Host adapter 的数据完整性 guard：不写回 canonical source，不增加恢复/执行授权，不改变 Owner、async 接受边界、资源作用域或 ADR-0055 的因果屏障。未发现新增 Standards 或明确 smell 问题。静态复核，未运行测试或修改源码/测试。
