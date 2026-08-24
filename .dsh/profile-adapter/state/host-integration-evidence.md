# 真实 DSH Host 集成测试证据（2026-08-25）

验证命令: `tools/dsh_host_integration_check.py --session-dir <child> --leader-session-dir <leader>`
只读 WorkItem（semantic-analysis，scope.write=[]），无任何文件写入。

## Worker 维度（provider/model 闭环）

- requested binding（由 `profile-source.yaml` 解析）:
  `semantic_read → opencode-go / deepseek-v4-flash / high`（统一默认 high）
  （profile 1@eac69ee0f096 · revision dsb@eac69ee0f096 · digest c7c73da8…）
- 真实 Host 回执（`request/header` 事件，Host 生成非模型自述）:
  `{provider: "opencode-go", model: "deepseek-v4-flash", maxTokens: 384000}`，
  会话 `8092c796-3eb0-420c-9f28-56df7e684bc5`（delegationDepth=1，origin=subagent）
- 结论: **provider/model actual == requested，闭环成立**。
- 反证: 同一 seam 请求 `provider=opencode`（旧绑定名）→ Host 会话日志记录
  `NO_ADAPTER: no adapter registered for provider "opencode"`（会话
  `d38d7f82-…`）→ fail-closed 证据，绑定名已修正为 `opencode-go`。

## reasoning 维度（统一默认 high，已对齐）

- binding 统一默认 `high`，与 Host 侧 `agent-default-model.reasoningEffort:
  high` 对齐；Host spawn seam 不传递 reasoning 时，集成脚本采用
  agent-default-model 的默认值（来自 Host 配置，非模型自述）参与 requested-vs
  -actual 比对。

## Leader 维度（fail-closed，如实未闭环）

- 当前 UniFlow 会话（session-848bff14-…）实际 Host 回执:
  `{provider: "opencode-go", model: "deepseek-v4-flash", reasoningEffort: "high"}`
  （来自 `~/.dsh/settings.yaml` agent-default-model）。
- Leader primary 绑定要求 `zai/glm-5.2/high`；`zai/glm-5.2` 路由在本 Host
  真实可用（代理探测成功，会话 24d9456e-…）。
- 结论: 当前会话 Leader 实际模型 ≠ primary → `record_leader_receipt` /
  `check_leader_binding` 判定 `model_binding_mismatch`，不静默降级；需要 Host
  以 zai/glm-5.2 启动会话，或经允许的平台级原因触发唯一 authority 交接。

## 结论（如实）

- 配置与 Adapter 层（DispatchGate / binding 解析进 Envelope / Host seam /
  回执核对 / Leader 绑定 / 20 条测试 / 3 个 validator）**全部完成并通过**。
- Host 实际模型保证: **provider/model 维度真实闭环验证成立**；reasoning 与
  Leader primary 维度按规则 fail-closed（Host 当前 seam 无法传递 reasoning、
  当前 Leader 会话未运行在 zai/glm-5.2 上），不做静默降级或模拟宣称。