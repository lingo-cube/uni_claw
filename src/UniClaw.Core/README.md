# UniClaw.Core 代码索引 — 跨域候选词汇集

Kernel 与 Host 共享的**候选词汇 record 集**：四词为候选词汇、不锁最终
枚举（CoreRecords.cs 头注）。语义权威仍在各 owner（Kernel 六 L2 /
Capability Plane）；本程序集只提供跨组件传递的 typed 不可变形状，
不含任何判断或行为。

消费方：Kernel `Core/CoreSemanticProjection.cs`（Kernel→Core 投影缝，
CORE-002/007）与 `tests/UniClaw.FileSystemRealization.Tests`（真实文件
系统锻炼 Core 形状）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `CoreRecords.cs` | 跨域候选词汇 record 集（EvidenceRecord/Slice 等候选形状；不锁枚举） |

## 变更规则

只加共享形状，不加行为；新 record 若已属某 L2 权威语义，先进 Kernel
对应维度，再在此投影。
