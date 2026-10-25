# 第四片：释放文件 writer 的重复历史缓存

原 Control/RuntimePlan 的 `FileCommittedHistory` 和 `FileRunStateJournal` 在落盘确认后仍永久保存每个序列化 payload；领域 Owner 已有自己的业务状态。现在 writer 仅保留已确认序号、RecordingId、hash，公开 `ReadRecords` 按需从原文件严格读取并核对 writer 的已确认端点。读/写互斥、旧返回值独立、只读 reader 缓存行为保持。文件格式、head/hash、批量及取消/关闭提交屏障没有改变。

真实 RED：`control-red.json` 的单例保留 54,825,616 bytes；同例 `control-green.json` 为 4,325,624 bytes。Run 的 `run-red.json` 为 50,709,184 bytes（另6例通过），同7例 `run-green.json` 全通过，Run为267,080 bytes。这里是串行collection内全进程GC后的差值，不是峰值/精确对象占用，不把单次结果推广成稳定比率。最终209例批次的Control/Plan/Run分别720,264 / 4,896,544 / 720,360 bytes，均低于24MiB上限。

扩大回归最初188/190，完整失败保留在 `regression.json` 对应Run：取消/关闭资源夹具缺少v4必需初始Snapshot；另一个是真实超限失败未锁定缺陷。补齐合法Identity/Snapshot且保留原断言后，`resource-limit-red.json` 仍为2通过1失败；将freeze/超限guard移入资源原fault catch后，最终 `regression-final.json` 为209/209、零跳过。取消、Close、batch、v1/v2恢复、源绑定/损坏以及真实进程Kill由这次所选原测试共同验证；未重跑全产品/设备/模型，不冒称隔离纯候选或父A1–A10全部完成。

两轴独立评审均0项：`review-spec.md`、`review-standards.md` 是原两生产文件及新测试；`review-spec-delta.md`、`review-standards-delta.md` 是实际回归后资源修复扩展。对比入口见 `entry.json` / `entry/`，最终精确diff是 `candidate-final.patch`。不使用整个共享HEAD diff归属本片。

复验入口：`python3 changes/RUNTIME-STORAGE-002/evidence/slice4/run-tests.py <唯一新标签> '<test filter>'`。使用当前共享源码及 `tools/test_assets.py`，每次新Run保留源码/输入指纹、程序集摘要、日志和TRX，全部消费者完成后登记回执。已记录运行的源码/输入在各次执行期间均未变化。最终所选filter及配置/Run位置以 `regression-final.json` 和 `changes/RUNTIME-STORAGE-002/assets.json` 为准；旧记录不随当前本地profile迁移。

本次使用配置父根 `/Users/fran/.local/share/uniclaw/test-assets`，工作相对路径 `work`、仓库键 `uni-claw`，再拼Change/Run及 `logs` / `results` 分类；配置digest `24f3a08b6fcd30ebd664c0068078aab1f8433824632d1ac212417f46933c0615`。本片调用其现有实现，配置工具归另一聊天所有。

成本取舍：正常写入不长期缓存重复payload；显式writer历史读取新增完整文件I/O及原严格验证，仍会物化完整结果，未优化只读reader峰值或全链扫描。没有磁盘体积/吞吐/断电结论，也不对所有记录族强制新latest API。World盘点见父evidence/world-buyer-inventory.md：现有生产读取需完整前缀、尚无独立latest buyer；父辅助快照/维护与总体验收继续open。

资产收尾：本片未创建独占构建树或完整仓库副本，仅保存4个必要入口源和精确diff。每次Run由AssetRun完成逐文件bytes/SHA256及实际保留回执，保留RED、GREEN、初次回归失败和最终结果用于后续审查；没有多余临时树需要删除，deletedBytes=0，所有运行已停止。旧片证据不改写，未提交/推送。
