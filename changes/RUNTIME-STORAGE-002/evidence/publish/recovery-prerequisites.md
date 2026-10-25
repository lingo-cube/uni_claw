# RUNTIME-STORAGE-002 Host recovery-read prerequisites

## 做了什么

只读检查 `FileRunRecoveryArchive.cs:91/95/97/99/105` 的五个缺失符号，沿 Kernel 当前定义追到相关 closed Change 的 scope/ownership。未构建、测试、修改源码或索引。

## 原来的问题

private view 的 Kernel 已通过，而 Host 的 `FileRunRecoveryArchive` 被选入后，仍缺少它作为“文件 adapter → Kernel 只读 reader”所调用的公开 Runtime 类型。错误不是 File adapter 应该自行重写读取语义，也不授权加入完整 resume。

## 最小原子路径组

### 直接编译闭包（本轮必须）

以下四个 Kernel 文件共同提供五个缺失类型；它们须与已入选的 `src/UniClaw.Host/Runtime/FileRunRecoveryArchive.cs` 一起进入 private index：

1. `src/UniClaw.Kernel/Runtime/RunRecoveryRead.cs`
   - `RunRecoveryRead`；两方 Run/Resource 只读结果。
   - 来源：`RUNTIME-RECOVERY-005`（closed）。
2. `src/UniClaw.Kernel/Runtime/RunEvidenceRecoveryRead.cs`
   - `RunEvidenceRecoveryRead`；三方 Run/Resource/Evidence 只读结果。
   - 来源：`RUNTIME-RECOVERY-007`（closed）。
3. `src/UniClaw.Kernel/Runtime/RunWorldRecoveryRead.cs`
   - `RunWorldRecoveryRead`；四方 Run/Resource/Evidence/World 只读结果。
   - 来源：`RUNTIME-RECOVERY-010`（closed）；其 World history reader 前置来自 `RUNTIME-RECOVERY-009`（closed）。
4. `src/UniClaw.Kernel/Runtime/RunRecoveryReader.cs`
   - `RunRecoveryReader` 与 `IRunRecoverySources`；将 archive identity 和 caller-owned 的 Run/Evidence/World/Control/Plan sources 编排为只读查询。
   - 来源：`RUNTIME-RECOVERY-015`（closed）；Runtime Plan 的 default source 兼容接口扩展来自 `RUNTIME-RECOVERY-016`（closed）。

这正好覆盖五个 Host 错误：91=`RunRecoveryRead`、95=`RunEvidenceRecoveryRead`、97=`RunWorldRecoveryRead`、99=`RunRecoveryReader`、105=`IRunRecoverySources`。

### 协议配对组（必须核对已经在 private Kernel view 中；缺失时按整组补）

直接四文件不应被当成可孤立移植的 DTO。它们调用/读取下列既有 Owner contract；既然 Kernel 已通过，预期它们已在 view 中，但操作者必须以 allowlist 检查为准：

| 所属层 | 配对路径 | 归属 | 不可替代的原因 |
|---|---|---|---|
| 两方共同点 | `Runtime/RunResourceAccounting.cs`、`Runtime/RunResourceRecovery.cs`、`Run/RunModel.History.cs` | RECOVERY-005 | `RunRecoveryRead` 的 exact committed endpoint 由既有 resource binding/history 认证；不能由 Host 用文件名或独立读取代替。 |
| 三方共同点 | `Runtime/RunResourceAccounting.cs`、`Evidence/EvidenceLedger.History.cs` | RECOVERY-007；Evidence identity/history 仍受 ORIGIN-CORRELATION-001 约束 | `RunEvidenceRecoveryRead` 要精确 Evidence history position；不从 canonical 数量或 Run progress 推断。 |
| 四方共同点 | `Runtime/RunResourceAccounting.cs`、`Runtime/RunResourceRecovery.cs`、`World/WorldHistory.cs` | RECOVERY-009/010 | `RunWorldRecoveryRead` 仅认证完整 original endpoint；不恢复 `WorldModel.Current`。 |
| source abstraction | `Runtime/RunRecoveryReader.cs` 加上现有 `RunResourceRecovery.cs` 与 `Control/TraversalControl.History.cs`、`Runtime/RuntimePlanHistory.cs` | RECOVERY-015/016 | `IRunRecoverySources` 是单 Run 的五类历史 source seam，不是通用存储接口；Host 负责 File adapter/lifetime。 |

## 这次修改/索引建议

1. 先向私有 index 加入“直接编译闭包”四个 Kernel 文件，不重写或移动 Host 的 `WithReader`/`FileSources`。
2. 检查上表协议配对路径是否已经在 Kernel private view；若任一缺失，按所属 closed Change 的整套最小 contract 补入，而不是为 Host 删除 `Inspect*` 入口或伪造简化 record。
3. `FileRunRecoveryArchive.cs` 本身是跨多个 closed recovery slice 的 Host adapter。若 private index 是 hunk 级而非文件级，保留其 `WithReader`/`FileSources`（当前约 91–125）这一整段，并避免混入无关 Host dirty hunk。

## 验证结果

* 当前 `RunRecoveryReader.cs` 同时定义 `IRunRecoverySources` 与 `RunRecoveryReader`；其三个 inspect API 分别返回 `RunRecoveryRead`、`RunEvidenceRecoveryRead`、`RunWorldRecoveryRead`。
* `RUNTIME-RECOVERY-005/007/010/015/016` 均为 closed，且 ownership/state 分别把上述两方、三方、四方和抽象读取 seam 限定为历史只读。
* 所有 result 类型均显式 `ContinuationSupported => false`；这组不会也不得扩展为 current/activation/Drive/handler resume。
* 本任务禁止 build/test；因此只报告静态路径闭合，不声称 Host 已通过。

## 新发现与边界

`RUNTIME-RECOVERY-008` 和 `011` 是审计片，不能当作运行时 provider；`009` 才拥有 `WorldHistory.cs` 的原历史实现，`010` 才把它接入四方共同点。`016` 只给 reader 增加 original Plan source 的可选查询，不授权完整 execution cursor 或 handoff 恢复。

## 下一步

按四个 direct paths 重新形成 private view；若仍有 Host 错误，再以其具体 consumer 分类为遗漏的 closed contract 或误带的 caller，不能扩大到整个 Host/Kernel 或完整 resume。
