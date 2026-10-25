# 联合发布范围盘点（只读）

## 结论
当前分支 `uni-harness` 跟踪 `origin/uni-harness`；检查时 index 无暂存项、无 unmerged path，工作树约 1,898 条状态记录。该统计含本审计文件写入后的状态，目录内的 1,892 属近似快照。不得用目录级 `git add` 或整仓暂存。机器清单见同目录 `path-inventory.json`：候选来源来自 RUNTIME-STORAGE-001/002 各 slice 的 candidate/entry manifests 与资产 AUTOMATION candidate manifests，列出 41 个代码/工具/测试/文档路径；9 个在 HEAD 已跟踪、32 个 HEAD 未跟踪。

## 候选路径与归属
- 存储候选（逐文件以 manifests 为准）：`src/UniClaw.Host/Runtime/*` 中 FileRunStateJournal、FileRunResourceJournal、FileCommittedHistory、FileWorldJournal、FileWorldJournal.Versions、WorldRevisionDeltaCodec、AuxiliaryArtifacts 等；`src/UniClaw.Host/HostRunner.cs`；Kernel 侧 `EvidenceLedger*`、`KernelRunDriver*`、`World*`、`RunModel*`；配套 Host/Kernel tests 与 `tools/RunRecoveryProbe`、`tools/WorldStorageProbe`。slice4、slice5、world-baselines 的 entry manifests 给出了精确的路径和摘要。候选清单并不等于本轮均已完成验收；剩余 Host/World/Coverage 由主线完成验收后再裁决。
- 已交接资产候选：`tools/test_assets.py`、`test_asset_operations.py`、`test_asset_archives.py`、`test_asset_cleanup.py`、`tools/test-assets`、`tools/verify-change`、对应 `tools/tests/test_*.py`，以及 DSH `RuntimeToolHost.DevelopmentAssets.cs` / `RuntimeToolHost.cs` / `RuntimeHttpServer.cs` 和相关 DSH tests；文档 `docs/agents/test-asset-retention.md`、`tools/README.md`。以 AUTOMATION slices 的候选哈希/owner 标注及 LAYOUT/AUTOMATION state 的边界为准。LAYOUT 已闭合；AUTOMATION 尚未关闭，不能据此把整 Change 相关文件一概纳入。
- `path-inventory.json` 的 `tracked_at_HEAD=false` 是构建/提交前置核查清单：这些源文件、测试、Python 工具目前未在 HEAD，不能假定只提交修改差异即可带齐依赖。该字段不是提交裁决。入口共享文件（如 HostRunner、RuntimeHttpServer、RuntimeToolHost、Program、KernelRunDriver、UniKernel、README、test_assets.py）可能混入多个 Change 的 hunk；逐文件比对候选摘要、当前 diff 与所有者后再精确分块，不能整文件自动归属。

## 证据体量与风险
按文件 metadata 统计三项 Change evidence 中 >=1 MiB 文件共 43 个，明细和字节数在 JSON。最大项是 `RUNTIME-STORAGE-002/evidence/slice3/cost/legacy-256/resource.journal.jsonl` 40,323,675 bytes；另有 `slice1/pure-build-inputs.tar.gz` 11,494,823 bytes；STORAGE-001 多份重复 `full-v1` 原始树各 7,951,934 bytes、重复 baseline-entry-source tar.gz 约 4.18–4.26 MB；storage TRX 多份约 1.8–1.9 MB。sealed/candidate-source 快照、程序集/obj/bin、原始 journal/log、重复 TRX 和 tar.gz 可能是构建输入、历史对照或运行产物，不应盲目全量入 Git。此盘点只标风险，不删除、移动或改写；提交时优先以验收引用/最小摘要决定是否保留大件，保留受保护的基线和有效证据。

## Git 与下一步
当前 `git status --short --branch` 显示约 1,898 条 dirty 状态，包含其他工作流、删除项及大量新文件；index 空、冲突列表空、分支 upstream 已配置。候选快照多数以固定 base `a05176f3…` 比较单片差异，不能视作全树 ownership。主线完成 Host/World/Coverage 验收后，按 manifest 摘要、逐 hunk 归属、未跟踪依赖及 test-asset retention 状态建立明确 allowlist；隔离构建后再提交推送。此盘点未运行网络、构建或测试，也未 stage/commit/checkout/reset。

## 补充：最终候选文件与依赖闭包
已将清单补至 136 个路径（99 个在 HEAD 未跟踪），并给候选出处打上 `provenance`：STORAGE-002 slice1 `candidate-v6.json`（13项）、slice2 `candidate-v2/manifest.json`（27项）、slice3 `candidate-v4/manifest.json`（15项）及后续 entry manifests；STORAGE-001 按每片最终显式 candidate-input 路径清单（slice1 v2、slice2 v1、slice3 v1、slice4 v2、slice5 v1、slice6 v3，slice6 23项）加入。这里记录的是候选路径/摘要的出处，不把所有输入源快照或 `sourceHash` 当成 ownership。尤其新增了 `KernelRunDriver.cs` / `DiagnosticObservations.cs`、DSH report partial/endpoints/background-work/probe、资源版本 journal/delta、World journal codec/options/readers，以及 Runtime Plan、Run recovery 等明确候选。

静态引用核查指出几组明显闭包：
- `DiagnosticObservations` 与 `KernelRunDriver`、`RunDriverInputs` 和页面观察执行共享；candidate-v6 明确列出。
- DSH Reports partial 依赖同一 `RuntimeToolHost` partial、HTTP endpoint、background-work 生命周期、Host required-delivery/run-store、报告生成器和多个 `RunRecoveryProbe` 子探针。候选中明确的 `ReportDeliveryEndpoints.cs`、`RuntimeBackgroundWork.cs`、`ReportToolProbe.cs`、`ReportDeliveryProbe.cs` 不可漏掉。
- 资源版本实现需 `FileRunResourceJournal`、Versioned partial、`ResourceSnapshotDelta`/delta codec，以及对应回放/recovery interfaces；旧 `RUNTIME-RECOVERY-*` readers 是明显既有前置，不能仅凭储存候选源码重建。
- World version reader/writer 需 `FileWorldJournal` partial、revision codec/options、history reader 和 World baseline/recovery tests/fixtures；RUNTIME-WORLD-HISTORY-001/002 是相邻前置。
- Plan history 的 Kernel partial、RuntimePlanHistory、FileRuntimePlanJournal 与 control traversal/history APIs 构成一组；RUNTIME-PLAN-003/004 可能带来必要接口，也包含可能不属于本提交的 plan-generation 功能。
- Recovery probe 子模块共用 `RunRecoveryProbe.csproj` 与入口 Program；加入任一测试探针时需核对其余引用模块。以上是在明确 candidate paths 上 `rg` 声明/partial 引用的结果，不是对完整解决方案的构建证明。

主线隔离构建时应先确认这些未跟踪源码的所需前置均进入构建视图，并按原 Change owner/hunk 分开审阅。尤其不能把 `RUNTIME-RECOVERY-*`、`RUNTIME-PLAN-*`、`RUNTIME-WORLD-HISTORY-*` 的全部差异自动扩展成此次发布范围；这里只提示明确编译/接口闭包，Leader 仍须按前置验收与所有权裁决。
