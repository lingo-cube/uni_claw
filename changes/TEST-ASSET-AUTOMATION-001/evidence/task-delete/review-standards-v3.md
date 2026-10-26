# Standards review v3

11 项固定 SHA-256 一致。v2 回执问题已修：`_portable_receipt` 按旧 binding 映射资产位置，并递归投影诊断/引用中的 work、archive、repo 与 binding-registry 根；持久化和返回值同样处理。缺失 legacy JSON→pending 的 RED/GREEN 证明回执不含 repo 绝对路径，且保留 `missing.json` 诊断。

未发现新增硬违反或可操作异味。仅查阅 regression-v4（95 通过）及 actual-cli-v4 受控证据；未运行测试。
