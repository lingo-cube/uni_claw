# 总验收补正：World完整历史读取

128次真实World观测的同内容成本测量发现：v2文件1,107,547 bytes，v1为7,951,934 bytes；但当前v2 constructor+ReadRecords分配159,316,504 bytes，v1为115,236,592 bytes。固定顺序、单次wall time仅作观察，不能据此宣称稳定速度优势。

具体重复是FileWorldJournal只读构造器解码v2后把完整row重新Serialize为字符串，ReadRecords再Deserialize。复用已经验证和解码的不可变WorldHistoryRecord/JsonElement，可移除往返；每次返回独立数组，调用者改数组不改reader缓存。reader关闭后原返回数据仍可用。

本次公开文件adapter读取缝沿父state已接受。先在同一128观测用例规定native完整读取的current-thread managed allocation小于128MiB，形成真实RED；再移除重复往返并GREEN。保留原每条JSON字段/值/顺序断言，v1/v2兼容、初始化/序号/hash/EOF/head、delta形状、跨来源和严格完整恢复校验不变。不增加latest/snapshot/恢复授权，不截链、不删除。

中间候选仍分配137,120,000 bytes，未过原预算，保留52/53实际失败结果。继续去掉ReadToEnd/substring/Split对整个文件的重复字符串：逐行解析，并在读取前后保留原空/末行换行检查及同一完整hash/head校验。不放宽预算或改原JSON断言。

这只补一个实际完整历史reader的成本缺口；父A4核心族资格、A5基线/链长策略、独立Coverage交付等未完成项继续明确保留，不能以本优化代替验收。候选入口源在evidence/acceptance/entry，Leader拥有产品文件及本轮测试增量，不改其他任务源/旧封存。
