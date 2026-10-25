# 已验收资产治理发布清单（只读盘点）

本清单只盘点 LIFECYCLE（closed）、LAYOUT（closed）及 AUTOMATION 已验收 slice1–4 的接线；不构成 AUTOMATION 关闭。规范真相源是 `docs/agents/test-asset-retention.md`，LOCAL UniFlow 与三个本地扩展负责接线，`tools/` 负责 test-assets.v1、verify-change、重放/诊断、archive/restore 和 opt-in `release-work`。

## 发布边界

AUTOMATION 仍 open：任务级联删除、自动临时 GC、存量迁移和产品存储回收未实现。slice4 仅验收 opt-in release-work（65 项 Python、两轴有界通过）：旧 100 个物理文件保留，新 6 个验证副本归档后释放且可恢复。不能把本机 `~/.config` JSON、外部 rawRun 目录或 `bin/obj` 纳入 Git 发布；配置/绑定与外部原始数据另行保留，编译缓存不是发布依赖。

## 文件与集成规则

JSON 清单含每个文件的当前 SHA256、候选/证据引用和集成边界。`RuntimeToolHost.cs`、`RuntimeHttpServer.cs`、`tools/README.md`、`.agents/skills/uniflow/SKILL.md`、`schemas/README.md` 等共享文件必须按候选 patch 的相关 hunk 集成，不能整体暂存；其余工具与专用测试可按对应 slice 候选文件核对。`RuntimeToolHost.DevelopmentAssets.cs` 是专用开发资产 seam，但仍以 slice2 candidate 为准。

对应证据：`changes/TEST-ASSET-LIFECYCLE-001/state.md`、`changes/TEST-ASSET-LAYOUT-001/state.md`、`changes/TEST-ASSET-AUTOMATION-001/state.md`；候选索引为 slice1/candidate.json、slice2/candidate-v2.json、slice3/candidate.json、slice4/candidate.json（均在 AUTOMATION evidence 下），差异边界为对应 candidate.patch/candidate-v2.patch，运行/审查报告见各 slice README 与 verification/finalization 文件。

补齐的实际导航与LOCAL参考文件共31项源/依赖，精确SHA和路径见JSON；Lifecycle资产相关hunk另存integration-lifecycle.patch，不包含第三方diagnosing-bugs改动。schemas/diagnosis-packet.schema.json是既有真实消费者依赖，不是本片新改schema语义。各Change状态/证据及必要WorkItem按显式引用一并保存；assets.lock等锁文件、缓存、本机profile/绑定和仓库外Run数据不暂存。
