# RUNTIME-STORAGE-002 Slice 3 — Standards review v2

**范围与结论：PASS（未发现新增且需处置的 Standards/smell 缺陷）。** 仅审`candidate-v2/manifest.json`与`owned.patch`的 15 个声明文件；不以共享 dirty HEAD 或封存外产物归因，也不把测试源码当成已执行证据。

## v2 变更复核

- 原五参构造器保留，并委托给 private core constructor；`ResourceVersionOptions`走独立、显式的 overload。因此既有调用的 ABI/参数含义不改变，version 查询配置也没有扩散到 Kernel/Owner interface。`_headPath`改由绝对`_path`派生，和同样使用`_path`的 baseline 目录一致，消除了相对 journal 路径下 head/baseline 不同根的风险。
- v4 全历史读取在首 frame 增加 Initialize source-prefix 双空校验。它位于`ReadVersionRecords`，即`ReadRecords`、`Inspect`与`InspectRecovery`所用的严格路径；快速`ReadVersion`仍按契约只读版本数据而不认证未展开历史，不能被误称为恢复许可或完整历史认证。该分层符合本片 `resource-version-contract.md` 与架构的 read model 不回写/不取得 authority 规则。
- 接受后的 async 语义未改变：token 仅在进入 gate 前观察；获 gate 后 frame flush、baseline（如需）和 head 发布必须结算，失败锁 writer、尾部不获 head 确认。没有跨 Begin/Confirm 收集批次。新增取消、head 发布失败、CreateNew 重开和子进程 Kill 的测试都围绕这些原边界；ADR-0055 的因果屏障和确认要求仍成立。
- 递归列举/导出 baseline 只用于测试证据完整性，未成为产品清理、恢复追加或新 Owner。v4 的 snapshot/head/frame 限长、tail record/byte 阈值与全路径 SHA 对照保持；成本说明明确 CPU/分配增加而只声明存储/读取侧下降，没有“全部指标更快”的越界结论。

## Smell judgment

未见有具体后果的 Mysterious Name、Duplicated Code、Feature Envy、Data Clumps、Primitive Obsession、Repeated Switches、Shotgun Surgery、Divergent Change、Speculative Generality、Message Chains、Middle Man 或 Refused Bequest。资源私有 schema/codec 与小型 version value types 均服务于已存在的 journal buyer；不应为此抽取公共泛化平台。

## 验证边界

这是静态 Standards 复审，未运行测试、模型或设备，未修改 production、tests 或封存候选。
