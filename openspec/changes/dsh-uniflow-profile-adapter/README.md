# dsh-uniflow-profile-adapter

**Status**: APPLY in progress. DSH-side adapter consuming the UniClaw Profile
Core for heterogeneous coding workflows (GLM-5.2 leader, DeepSeek-V4 workers).
Apply increment 2026-08-25: 强制 WorkItem 派发门 / ModelBinding 解析进 Envelope /
Host 派发 seam 与能力 fail-closed / 实际模型回执 / WorkResultGate 回执核对 /
Leader 绑定（见 `tasks.md` A1–A10 与 `spec.md` 新增 Requirements）。

## One-line

DSH consumes `.ai/profiles/*.json` + WorkItem/WorkResult schemas +
`agent_profile_validator.py` semantics via a read-only Profile Source, adds
model binding / routing / context loading / checkpoint / events — without
redefining any upstream semantics.

## Why

`AGENTS.md` §4 UniFlow requires DSH to consume the same Profile/WorkItem
semantics as Codex; `.ai/workflows/codex-coding-workflow.md` §10 left the DSH
adapter unspecified.

## What changes

- `tools/dsh_profile_adapter.py` — loader/adapter/binding/router/context/cache/checkpoint/envelope/result-gate/events + DispatchGate/DshHostClient/Host receipts
- `tools/dsh_host_integration_check.py` — 真实 Host 最小集成测试（只读 subagent + 回执核对）
- `.dsh/profile-adapter/profile-source.yaml` — pinned source config + model bindings（provider 名按真实 Host 路由 `opencode-go`）
- `tests/AgentWorkflow/test_dsh_profile_adapter.py` — 30-case gate suite
- `tests/AgentWorkflow/test_dsh_uniflow_gateway.py` — 强制闭环 20 条（任务 §九）
- `.dsh/profile-adapter/README.md` — usage + minimal example

## Boundaries

No edits to `.ai/` upstream truth; no second WorkItem/WorkResult; no new Agent
Runtime; no fanout; tool-only never invokes a model.

## Validation

- `python3 tools/dsh_profile_adapter.py validate`
- `python3 -m unittest discover -s tests/AgentWorkflow -p 'test_*.py'`
- `bash scripts/check-consistency.sh`
