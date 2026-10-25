# RUNTIME-STORAGE-002 Slice 3 — Standards review v4

**Delta 结论：PASS。** 继承 v3，仅复核`candidate-v4/delta-from-v3.patch`。

`IsVersioned`先确认 JSON 根为 object、存在`schema`且其为 string，才比较 v4 schema；`{}`、`[]`、数值 schema 会留在原 legacy/invalid reader 路径，由公开`Inspect`的既有受控异常映射返回`resource-history-unreadable:*`，不再泄漏 `KeyNotFoundException` 或 `InvalidOperationException`。三例回归恰好覆盖这些输入形状。

该变更只收紧 Host adapter 的格式探测，不改变存储事实、Owner/authority、async 确认、资源作用域或 v3 的 head endpoint guard。未见新增 Standards/smell 问题。静态复核，未运行测试或修改源码/测试。
