## Why

Foundation §11 要求 TUI 消费同一 Query/Analysis Core、禁止各自实现逻辑；"UI framework 先根据仓库技术栈调查，不先拍脑袋选"。调查结论：仓库 Python 工具层为 stdlib-only（无 pyproject/依赖基线），textual 8.2.8 可经 uv 秒装且提供 Tree/面板/绑定键。P3 交付 TUI 薄壳 + 可测视图模型层。

## What Changes

- 技术栈决策：`textual`（v8）作 TUI 框架，仅限 `tui/app.py`；`tui/view_models.py`（open_run/tree_view/filter_state/diagnosis_view）为纯 stdlib 视图模型，从 Query Core 派生全部可见数据——**TUI 零分析逻辑、Core 包本体保持零框架依赖**。
- `runtime-debug-tui <bundle-dir>` 薄壳：EXECUTION/CAUSAL 树（t/c）、errors-only 过滤（e）、AssetRef 面板（a）、诊断面板（FAILED spans，d）、退出（q）；textual 导入延迟到 `main()`（模块无需框架即可编译/测试）。
- 测试：5 项视图模型单测（open_run 从 bundle 派生事实、filter_state 构造剪枝参数、tree_view 确定性展平、diagnosis_view 呈现 FAILED span、模块导入不依赖 textual）。
- 本项目不做图片渲染（terminal 图像协议差异大）、不做完整 diff 交互（core 命令仍可从 CLI 使用）——列为 deferred。

## Capabilities

### New Capabilities

- `runtime-debug-tui-shell`: 同一 Query Core 之上的薄 TUI 壳 + 可测视图模型（渲染/输入收集，无分析逻辑；框架依赖隔离于 app.py）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/tui/{__init__,view_models,app}.py` + `tools/runtime-debug-tui` 入口 + README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +5 视图模型测试。
- 无 Runtime/Harness/wire/Trace 变更；runtime_debug 核心包保持 stdlib-only；textual 仅 TUI 运行期可选依赖（uv --with textual）。
