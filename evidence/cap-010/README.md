# CAP-010 证据 — capability 抽样检查收口

四元组与原始输出索引。变更面：两个文档（docs/capability-hub/README.md、
customization-integration-development-protocol-v0.1.md）+ 一个新增测试文件
（tests/UniClaw.Kernel.Tests/Capability/ModelRoutingReplaceabilityTests.cs）。
未改任何产品源码，未动公开面，未动场景文件。

## A1/A2 — 文档修正与升格（DIFF 即证据）

```sh
git diff docs/capability-hub/
```

- README「当前 Change State」由 CAP-001 → CAP-008（注明 CAP-006→008 谱系与
  在途 CAP-009）；「验证证据」行同步现实（cap-001/cap-005 目录 + 四元组在
  各 state.md）。
- 协议指南头部 DRAFT/NONE → FROZEN / PROTOCOL GUIDE v0.1 + Authority:
  COMPONENT（架构权威从属声明在头部）。

## A3/A4 — R5 测试与执法套件

```sh
dotnet test tests/UniClaw.Kernel.Tests \
  --filter "FullyQualifiedName~Capability|FullyQualifiedName~DocsMetadata|FullyQualifiedName~KernelRuntimeSurfaceWhitelist"
# expected: 全部通过（含 ModelRoutingReplaceabilityTests 3 项）
# actual:   已通过! 失败: 0，通过: 102（2026-10-07）
```

## 全量回归

```sh
dotnet test UniClaw.Kernel.slnx
# expected: 全部通过
# actual:   复跑 1386/1386 绿（见 full-solution-tests-rerun.txt）
```

备注：首次全量运行时 ScenarioCertificationTests 出现 2 例失败（工作区含
在途 SIM-006/CAP-009 的 SCN-*.json 修改，并行首跑偶发）；单独重跑该类 5/5
绿，全量复跑 188/188 绿，不再现。本变更不触及 scenarios/ 与 Simulation
读取的任何文件，判定为既有在途工作的环境噪音，非本变更引入。

## 文件

- `full-kernel-tests.txt` — Kernel.Tests 全量 829/829。
- `full-solution-tests.txt` — 首次全量（含偶发失败）。
- `full-solution-tests-rerun.txt` — 复跑全量 1386/1386 绿。
