# PROTOCOL-MACHINE-003 实现计划

状态：已完成（PROTOCOL-MACHINE-003 closed）。

1. ✅ 字段与 Owner 对照：P24 四字段、activation key、P25 四元组和 journal 状态映射到现有机器与 payload 引用。
2. ✅ 状态机补齐：增加 activation record 对象机，补 P25 retry/no-response 分支，修正 contract-view admitted hang。
3. ✅ 契约同步：更新启动 wire schema、protocol map、domain cards、基线 Deferred ⑰ 引用和相关分析文档。
4. ✅ 生成与验证：生成器、图审计、schema/reference checks、全解、插件、场景认证全部通过。

停止条件：发现需要新增 P24/P25 语义、跨 Host resume 或 deferred 能力时停止并回到 RESOLVE，不在本 Change 猜测。
