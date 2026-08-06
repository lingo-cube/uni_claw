# Shadow FSM Analyzer 脚本库

本目录包含 shadow-fsm-analyzer agent 的可复用分析脚本。脚本由 agent 自己编写和维护——当分析模式复用 ≥2 次时，写成脚本。

## 约定

- Python 3.11+（`.venv-local-vision`），零新依赖（stdlib only）
- 每个脚本自带 docstring：用途、输入、输出、示例
- `--help` 由 argparse 生成
- 只读：读取需求文档 / 测试文件 / run 目录 / trace 文件 → stdout（机器可读）或 stderr（诊断）
- 退出码：0=成功完成, 1=未找到数据/无结果, 2=用法错误
- 新增脚本后更新本文件

## 脚本目录

| 脚本 | 用途 | 输入 | 关键参数 |
|------|------|------|----------|
| `test_contract_extractor.py` | 🔑 从测试代码提取 FSM 契约（转移矩阵、handler 行为、门限值）——不读 C# 源码 | `--test-dir <dir>`（默认 `tests/UniClaw.Core.Tests/StateMachine/`） | `--json` / `--transitions-only` / `--thresholds-only` / `--check <expected.json>` |

> ⚠️ **`test_contract_extractor.py` 已知局限（2026-08-05 首次全量验证）**:
> - `valid_transitions` 通过顺序追踪 `TransitionTo` 链推断——**包含测试驱动路径**，把拒绝边（如 NodeSelect→Execute）误报为 valid。不可当矩阵结论。
> - `handler_returns` 按 before_context 猜测 handler 名，不可靠（含 DriveTo 辅助方法的假关联）。
> - 可靠输出：`thresholds`、`last_error_lifecycle`、`consecutive_error_increment_sites`、方法名级线索（`Step_*_GoesTo*`、`TransitionMatrix_*_Rejected`）。
> - **使用规则：矩阵结论必须人工校验测试源码**（参考 2026-08-05 lessons）。修复待后续：按测试方法边界切分 TransitionTo 链 + 排除 Assert.Throws 包裹调用。

## 复用规则

- **可复用 fsm-analyzer 脚本**：仅 `fsm_transition_path.py` 和 `fsm_cycle_detector.py`（它们读运行时数据，不读源码）
- **不可用 fsm-analyzer 脚本**：`matrix_from_source.py`（它读 C# 源码提取矩阵——破坏盲区约束）
- **在写新脚本之前**，先检查是否已有工具覆盖：TraceTool CLI、log-analyzer.py（项目级）、fsm-analyzer 的可复用脚本

## 与 fsm-analyzer 脚本的关系

| 维度 | fsm-analyzer 脚本 | shadow-fsm-analyzer 脚本 |
|------|------------------|------------------------|
| Ground truth | C# TransitionMatrix 字段 | 测试断言 + 需求文档 |
| 矩阵来源 | `matrix_from_source.py`（从源码提取） | `test_contract_extractor.py`（从测试推断） |
| 转移验证 | `fsm_transition_path.py --validate`（对照源码矩阵） | 对照自己的 fsm-design.md |
| 差距分析 | 源码↔文档 diff | 自己的设计↔测试契约 diff + 设计↔运行时 diff |
