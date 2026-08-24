# Tasks: dsh-uniflow-profile-adapter

> System of record. Human Gate approved by repository owner in the commissioning
> DSH session (readiness evidence: validator PASS @ eac69ee).

## Slices

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/tasks/README)
- [x] Slice 1 — `.dsh/profile-adapter/profile-source.yaml` (Profile Source + model bindings)
- [x] Slice 2 — `tools/dsh_profile_adapter.py`: ProfileSource/Loader/Adapter + version gate + events
- [x] Slice 3 — ModelBinding + fallback authority token + checkpoint restore
- [x] Slice 4 — Worker Router + ChangeSet scheduling (single owner, no fanout, no concurrent same-file writes)
- [x] Slice 5 — ModuleContext Loader + Profile Cache + delta acceptance
- [x] Slice 6 — Work Envelope + WorkResult Gate
- [x] Slice 7 — `tests/AgentWorkflow/test_dsh_profile_adapter.py` (30 cases)
- [x] Slice 8 — `.dsh/profile-adapter/README.md` usage doc + minimal example
- [x] Slice 9 — Validation: upstream validator + adapter validate + unittest + check-consistency

## Apply increment: 强制 WorkItem/模型绑定/回执闭环（2026-08-25）

- [x] A1 — DispatchGate：拒绝 Markdown/自然语言/缺字段/非对象 brief；tool-only
      fail-closed（不创建 Subagent、model=none、无写入/无语义判断）
- [x] A2 — ExecutionProfile → ModelBinding 解析（implementation_efficient /
      semantic_read / tool_only）写入 `dsh_work_envelope.model_binding`
      （role/provider/model/reasoning/profile_version/revision/digest/id/owner）
- [x] A3 — DshHostClient seam：从 Envelope 显式传 provider/model/reasoning；
      能力不足在任何修改前 `ROUTING_CAPABILITY_LIMIT`
- [x] A4 — 实际模型回执：`HOST_RECEIPT_FIELDS`、预执行核对、
      `read_host_receipt_from_session_log`（从 Host `request/header` 事件读取）
- [x] A5 — WorkResultGate 回执核对：`model_receipt_missing` /
      `model_binding_mismatch` 拒绝结果与 Delta，返回 ROUTING_CAPABILITY_LIMIT
- [x] A6 — Leader 绑定：`record_leader_receipt` + `assert_leader_primary`
      （zai/glm-5.2/high），不匹配 fail-closed；fallback 仅平台级原因且
      authority 唯一
- [x] A7 — 绑定 provider 名按真实 Host 路由修正为 `opencode-go`（
      `profile-source.yaml` + README + tests + spec/design 同步）
- [x] A8 — `tests/AgentWorkflow/test_dsh_uniflow_gateway.py`（任务 §九 20 条）
- [x] A9 — `tools/dsh_host_integration_check.py` 真实 Host 最小集成测试
      （只读 subagent；provider/model 与 requested 一致；reasoning 与 Leader
      primary 如实 fail-closed）
- [x] A10 — 验证：上游 validator + adapter validate + unittest + check-consistency
      全绿；真实 Host 集成结果如实记录
