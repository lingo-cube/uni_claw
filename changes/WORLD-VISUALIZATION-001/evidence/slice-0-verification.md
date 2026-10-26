# WORLD-VISUALIZATION-001 切片 0 验证记录

日期：2026-10-23。环境：Windows（本机 .NET SDK 10.0.402 用户级安装）；
基线 HEAD 56a75fd7 对照 worktree（..\uni_claw-baseline）。

## method

- 新测试类 `tests/UniClaw.Kernel.Tests/World/WorldVisualizationReadSeamTests.cs`
  （10 测试：历史快照稳定、单参兼容、未知 revision、空 World、时间线页/导航/
  纠错/语义标记、语义比较键稳定、结构关系快照、标签 join/降级/沿用）。
- 全量三套件 + 基线对照（Kernel/Host/Simulation 各自与未改动基线的失败集对比）。
- 认证与 pins：`python tools/scenario_certify.py --change WORLD-VISUALIZATION-001
  --all`（20/41 重封）+ ComponentReplay composition.json 显式重认证。

## expected

新缝行为全部被测试锁定；既有套件失败集与基线**逐项相同**（无新增回归）。

## actual

- 新测试 10/10 通过。
- Kernel.Tests：1107/1112 通过；失败 5 = 基线同款（VisionServiceHost ×4，
  Windows 无法执行 .sh 宿主脚本；SourceReadmeOwnership ×1，Windows 路径分隔符，
  基线同样失败）+ 1 个全量负载下时序 flaky（TraversalExecutionTests.
  CompletionBeforeDeadline，单测隔离复跑两次均通过，基线全量亦偶发）。
- Host.Tests：421/436；失败 15 与基线**完全相同的集合**（录制回放/组合/策略
  加载类，Windows 环境性）。
- Simulation.Tests：313/314；唯一失败 = 基线同款 AsyncHandler 时序；Component
  回放 22 例经 pins 重认证后全部通过（录制输入与独立 oracle 未改）。
- 场景认证 check：PASS（41 files, 0 violations）。

## 过程事故与处置（诚实记录）

- Component 回放守卫按**原始字节**校验源码 pins；本 Windows 检出因
  `core.autocrlf=true` 与 `.gitattributes`（*.cs eol=lf）冲突导致 989 个文件
  工作树为 CRLF，守卫拒绝。处置：`git config core.autrlf false` 修正为
  false，工作树逐字节归一化回 LF（内容零变化，`git add -A` 后幻影 M 全部
  消失，索引 100% LF 核实后执行）；基线 worktree 全程为干净 LF 佐证。
- 修复过程中两次被中断写坏的 obj/deps.json 已清理重建。

## evidence

- 本文件 + git 状态（35 个暂存变更 = 切片 0 全部产出）；
- 运行日志摘要见上（命令：dotnet test 三套件 / scenario_certify.py --check）。
