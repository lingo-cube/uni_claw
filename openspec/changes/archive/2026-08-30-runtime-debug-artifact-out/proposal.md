## Why

端到端暴露 UX 摩擦：`packet-generate` / `replay-extract` 产物在 envelope 的 `result` 里，必须 shell 提取才能喂回下游命令。`--out` 让生成物落盘成为一等操作（新文件、不在 bundle 内、不覆盖、原子写），pipe/工作流零提取。

## What Changes

- `packet-generate --out <path>` 与 `replay-extract --out <path>`：把 packet/fixture 本体以 canonical JSON 写新文件；错误语义：`INVALID_INPUT`（路径在 bundle 目录内 / 文件已存在，append-only）、`SCHEMA_VIOLATION`（写失败）；无 `--out` 时行为不变（产物仍在 result）。
- 契约测试 4 项：packet --out → summarize 读回、replay-extract --out → replay-run 读回、bundle 内路径拒绝、覆盖拒绝。

## Capabilities

### New Capabilities

- `runtime-debug-artifact-out`: 生成物（packet/fixture）的受限落盘（新文件、原子、不覆盖、禁入 bundle）。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/cli.py`（两分支 + `_write_artifact` helper）+ README。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +4 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖。
