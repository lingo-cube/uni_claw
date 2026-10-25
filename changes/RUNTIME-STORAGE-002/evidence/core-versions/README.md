# Control/Plan核心版本存储验收

## 行为与实现
原文件adapter每行保存完整Control累计State、Plan累计Versions，冷启动只能展开全历史。当前新写v2：原输入/结果/Graph等操作事实保持完整，State/Versions使用可逆结构增量；周期独立基线与原子head绑定原录制ID、序号、hash和日志位置。Plan物理可变表以有序键值数组编码，避免每次复制全部对象键名。旧v1严格兼容，不迁移、覆盖或删除历史。

FileControlTraversalJournal / FileRuntimePlanJournal的旧构造签名保留；新增options重载、ReadLatestVersion、ReadVersion(sequence,Forward/Reverse)。配置在产品构造注入HistoryVersionOptions：MaxTailRecords=64、MinDeltaBytesForSnapshot=65536、SnapshotByteRatio=4；以距上一基线的记录数，或编码尾段字节达到max(最小字节,基线字节×比例)触发。head索引、日志和`.versions/`同处传入产品路径，不读取开发资产配置。基线IO独立VersionWriteStatistics，原WriteStatistics保持合法两行batch一次frame write/flush/head确认。

data版本查询只验证所用基线/链及原末位，不认证更早前缀，不提供执行许可。完整ReadRecords/原领域恢复继续验证原初始化、全部逐条JSON/hash/顺序、来源和末位。writer查询遵守gate/fault及已确认端点；空writer完整历史仍为空，未确认失败不得发布latest。资源v4结构算法提取为共享ReversibleJsonDelta，typed resource wrapper保留原wire。

## 验证四元组
- method：实际Driver 128候选先9MiB净占用预算RED，再新旧公开文件reader、双向版本、旧v1、错基线/错源/损坏/未知版本、原提交/取消/批量/真实进程Kill定向验证。
- expected：原操作事实和值不变、基线与增量匹配同一确认、冷数据查询不展开全部历史、原严格恢复/失败屏障保持，计入全部逆向/快照成本。
- actual：最终相关Host159/159，零跳过；Spec/Standards各0。完整旧v4封存513记录经当前reader读取，原inputRecordSha256 `0A1C9A6342C4EC5B384EBA9961FA881D3F2EAADF1B00C1A191BC5FC434C2EEAD`保持，首/中/末正反版本一致、原文件摘要不变。
- evidence：versions-storage-final.json、review-spec.md、review-standards.md、resource-v4-compat.json、cost-comparison.json、verification.json；`python3 changes/RUNTIME-STORAGE-002/evidence/core-versions/verify.py`核对65项保留证据，不重新执行产品任务。

首轮中间候选81项38过43失败：readonly初读绑定时机错误、首帧误限64KiB、writer版本查询遗漏fault。均修正，原断言保留。该轮仅并行资产tools/test_asset_archives.py、test_asset_operations.py、test_assets.py发生漂移，所属C#输入稳定；最终验收运行全部源码/输入稳定。3份旧测试入口由反转所属hunks恢复，并与原RED provenance SHA256逐字节匹配；评审基点是captured entry，非共享dirty HEAD diff。

## 同一原记录/录制ID的成本

| 实际128场景 | v1日志+head bytes | v2日志+head+全部基线 bytes | v2 data latest查询计数 |
|---|---:|---:|---|
| Control候选增长 | 10,392,573 | 7,919,706 | 4帧+1基线，113,384 bytes |
| 不同Plan ID采纳 | 824,511 | 370,407 | 15帧+1基线，27,649 bytes |
| 同Plan ID升版 | 312,354 | 363,188 | 18帧+1基线，27,664 bytes |

等价v1文件来自同一次真实Owner产生的全部原记录，并由公开v1 reader核对一致；仅用于成本，消费后finally移除，不登记为原恢复来源。Control新45个基线共1,426,660 bytes；两个Plan场景各6个基线，分别18,794/5,189 bytes。小历史及同ID版表固定场景有额外开销，未声称全族压缩。

原前片初始化+Drive managed分配约Control227MB/不同Plan40MB/同Plan31MB，本片样本约274/60/35MB，编码/基线有新增成本；非稳定耗时、峰值内存或物理IO承诺。latest分配/时间实测包含新只读reader构造；ReadCost是查询自身计数，不含构造期格式探测及head检查，不冒称完整冷启动IO。

## 范围与资产
本片完成声明的Control/Plan核心版本存储，父A1–A10未全部通过；World独立基线和Coverage交付/附件保护交由既有聊天顺序接续。必要RED/中间失败/最终日志、TRX、raw原journal及紧凑结果按各独立AssetRun保留，分类目录经现有JSON解析绑定。测试自有目录finally清理，v1对照文件已移除，旧v4兼容探针独占临时编译目录移除4,762,078 bytes；旧封存源及他人材料未删，未造整源码树副本。未提交/推送，未调用设备/模型。
