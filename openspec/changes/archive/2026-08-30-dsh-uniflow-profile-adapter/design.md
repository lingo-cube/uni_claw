# Design: dsh-uniflow-profile-adapter

## Architecture position

```text
UniClaw Profile Core (.ai/profiles, .ai/schemas, agent_profile_validator.py)
        ↓  read-only
DSH ProfileSource (config: .dsh/profile-adapter/profile-source.yaml)
        ↓
DSH ProfileLoader (delegates to upstream validator; version gate)
        ↓
DSH ProfileAdapter (compose → AgentProfile; conflict → LeaderDecisionRequired)
        ↓
DSH WorkflowRuntime (router, scheduler, result gate, checkpoint, events)
        ↓
ModelBinding (zai glm-5.2 primary / opencode-go glm-5.2 fallback / deepseek-v4-flash worker)
```

## Key decisions

1. **Single-file Python tool, no new runtime.** `tools/dsh_profile_adapter.py`
   imports `agent_profile_validator` (same directory) for every semantic
   decision: `compose_profile`, `validate_work_item`, `validate_work_result`,
   `validate_change_set`, `build_context_manifest`, `resolve_module_for_path`,
   `profile_context_key`, `rule_digest`. DSH never re-implements upstream
   semantics — it wraps them and adds only runtime concerns (binding, envelope,
   checkpoint, events, cache).
2. **YAML-free config.** To avoid a PyYAML dependency, `profile-source.yaml`
   stores bindings in a JSON block under a `#BEGIN/#END JSON` marker; the
   loader parses that block. The file stays YAML-readable for humans.
3. **Envelope pattern.** `dsh_work_envelope` wraps the untouched WorkItem;
   `unwrap` returns a deep-equal original. No runtime fields ever enter the
   WorkItem (upstream `validate_work_item` would reject unknown fields — this
   is asserted by tests).
4. **Model binding.** Bindings live only in the DSH config. `leader_authority`
   is a runtime token: exactly one of primary/fallback holds it at any time.
   Fallback trigger reasons are an explicit allow-list (provider_unavailable,
   connection_failure, timeout, platform_tool_failure,
   structured_output_repeated_failure); business failures are rejected. On
   takeover, fallback loads the latest LeaderCheckpoint.
5. **Cache.** Keyed by upstream `profile_context_key`. The adapter holds the
   authoritative ModuleContext store (JSON on disk under
   `.dsh/profile-adapter/state/`); the model session is treated as cache only.
   Invalidation inputs: any profile version, rule digest, source revision,
   model binding version change, worker blocked, protocol violation.
6. **Checkpoint.** Minimal reference/summary document per spec; updated on
   accept/reject/dispatch; no reasoning, no worker transcripts.
7. **Events.** In-memory ring + JSONL append to
   `.dsh/profile-adapter/state/events.jsonl`, restricted to the 12 spec events.
8. **Result gate.** Ordered checks per spec; write-scope verification compares
   reported changed paths against `scope.write` using upstream `_path_allowed`;
   evidence checks verify non-empty verification list for DONE results.
9. **DispatchGate（强制 WorkItem 派发门）.** `dispatch_work_item()` 是 UniFlow
   唯一合法派发入口：拒绝 Markdown/自然语言/缺字段/非对象 brief；派发前执行
   Schema + Profile + 单一 owner + scope.write + 冻结决策 + 无未决架构 +
   ExecutionProfile 形态一致性校验；tool-only 不创建 Subagent、model=none，
   含源码/测试写入或语义判断请求即 fail-closed。
10. **ModelBinding 解析进 Envelope.** ExecutionProfile → binding role（
    development/test-authoring/verification → implementation_efficient；
    semantic-analysis → semantic_read；tool-only → tool_only）由
    `profile-source.yaml` 解析；provider/model/reasoning/revision/digest 写入
    `dsh_work_envelope.model_binding`，不改通用 WorkItem。
11. **Host 派发 seam 与能力 fail-closed.** `DshHostClient` 从 Envelope 显式读取
    provider/model/reasoning 传给 Subagent 创建；不支持时在任何文件修改/调度
    记录前抛 `ROUTING_CAPABILITY_LIMIT`。不回退到会话默认模型。
12. **实际模型回执.** Host 生成回执（session id / work_item id / worker_owner /
    actual provider/model/reasoning / binding revision / started_at），进入
    运行元数据与事件日志；模型正文自述不算。Worker 写文件前完成预执行核对。
13. **WorkResultGate 回执核对.** requested vs actual 逐字段比对 + id + owner +
    revision；缺回执（`model_receipt_missing`）或任一不一致（
    `model_binding_mismatch`）→ 拒绝结果、不应用 Delta、返回
    `ROUTING_CAPABILITY_LIMIT`，无静默 fallback。
14. **Leader 绑定.** `record_leader_receipt` 在 UniFlow 启动时记录 Host 提供的
    Leader 实际回执并断言 primary=zai/glm-5.2/high；不匹配即 fail-closed，
    不静默降级；fallback 仅接受平台级原因并保证 authority 唯一。
    bindings 图中 provider 名以本机 Host 实际注册路由为准：
    `opencode-go`（并行绑定事实随 `profile-source.yaml` 同步）。

## Testing

`tests/AgentWorkflow/test_dsh_profile_adapter.py` — unittest, same style as
`test_agent_profile_validator.py`, 30 cases mapping 1:1 to the spec scenarios.
All gates run against the real upstream validator and real profile files; no
semantic test doubles.

## Verification

```bash
python3 tools/agent_profile_validator.py validate
python3 tools/dsh_profile_adapter.py validate
python3 -m unittest discover -s tests/AgentWorkflow -p 'test_*.py'
bash scripts/check-consistency.sh
```
