# FSM Analyzer 脚本库

本目录包含 fsm-analyzer agent 的可复用分析脚本。脚本由 agent 自己编写和维护——当分析模式复用 ≥2 次时，写成脚本。

## 约定

- Python 3.11+（`.venv-local-vision`），零新依赖（stdlib only）
- 每个脚本自带 docstring：用途、输入、输出、示例
- `--help` 由 argparse 生成
- 只读：读取 run 目录 / trace 文件 / log 文件 → stdout（机器可读）或 stderr（诊断）
- 退出码：0=成功完成, 1=未找到数据/无结果, 2=用法错误
- 新增脚本后更新本文件

## 脚本目录

| 脚本 | 用途 | 输入 | 关键参数 |
|------|------|------|----------|
| `matrix_from_source.py` | 🔑 从 C# 源码实时提取 TraversalFSM + GlobalFSM 转移矩阵（ground truth） | 自动定位 `src/UniClaw.Core/StateMachine/*.cs` | `--json` / `--python` / `--check <expected>` / `--fsm traversal|global` |
| `fsm_transition_path.py` | 提取 FSM 转移序列 + 状态频次直方图 | `--run <dir>` / `--log <file>` / `--trace <file>` | `--validate`（从源码自动加载矩阵验证合法性）, `--matrix-file <json>`, `--json` |
| `fsm_cycle_detector.py` | 检测 FSM 循环（stuck_state / short_cycle / no_progress / error_loop） | `--run <dir>` / `--log <file>` / `--trace <file>` | `--threshold N`（默认 5）, `--json` |
| `fsm_run_metrics.py` | 从 run 目录提取 FSM/深度/滚动指标（scrolls by node、child_depth_limit_skipped、entries、steps）——跨 run 滚动策略对比（D-G11/P3 分析） | `--run <dir>`（自动定位内层 trace.jsonl） | `--json` |

### 实效性原则

- **矩阵不硬编码**：`fsm_transition_path.py --validate` 自动调用 `matrix_from_source.py` 从 C# 源码提取矩阵——永远反映代码最新状态
- **可缓存**：`--matrix-file <json>` 跳过源码提取，加速重复运行
- **可自检**：`matrix_from_source.py --check expected.json` 退出码 3 = 矩阵已变更（CI 可用）

## 复用优先

在写新脚本之前，先检查是否已有工具覆盖：
- `scripts/log-analyzer.py`（项目级）— run.log 表格/时间线/Mermaid/指标/对比
- TraceTool CLI（`src/UniClaw.TraceTool`）— diagnose/verify/timeline/diff/list
- MCP 工具（cwm-roslyn-navigator / csharper-mcp）— C# 符号查询
