# WI-RELC-003 Dispatch Record

DocumentType: `WORK_ITEM_DISPATCH_RECORD`
Authority: `NONE`
RecordedAt: `2026-08-25`
RecordedBy: `DSH coding agent (Sol Leader role, UniFlow session)`

## Dispatch facts

- WorkItem: `docs/work/active/workitems/WI-RELC-003.json`
- Validation: `tools/agent_profile_validator.py work-item` → `WORK_ITEM_VALIDATION_PASS`
- Routing: RoleProfile `module-worker` · ExecutionProfile `development` ·
  ModuleProfile `runtime-core` · worker_owner `luna-module-worker-1`
- Dispatch: single unicast subagent (no fanout), session-local id
  `b5abac9e-c1f7-4c8c-beba-b5bc90ef6a42`

## Platform adaptation record (workflow §10 obligation)

按 `.ai/workflows/codex-coding-workflow.md` §10，DSH 使用自身可用的委派能力
（subagent）消费同一 Profile 与 WorkItem，不复制另一套工作流。能力限制如下：

1. **模型绑定偏差**：项目 `model-routing.yaml` 对 development Worker 绑定
   `gpt-5.6-luna`（Codex adapter 路径）。DSH subagent 工具不接受模型参数，
   实际执行模型为 DSH `agent-default-model`（`zai` / `glm-5.2`，
   见 `~/.dsh/settings.yaml`）。Profile 的权限/边界/验收语义不受模型影响；
   模型差异已按 §10 显式记录。
2. **验收不受自述约束**：WorkResult 仅作证据；Leader 验收将独立重跑测试、
   重查路径与 spec 映射，不信任 Worker 自述（roles.json `worker_results_are_evidence_only`）。

## Acceptance plan (workflow §4 steps 8–10)

1. 独立核对 scope.write 路径外的文件未被触碰（git status 对照）；
2. 重跑：targeted exploration 套件 + Scenario 套件 + build；
3. 对照冻结 acceptance 逐条判定，特别是：
   - 真实路径不可分类节点零派发、不进 AuthorizedSiblingEvidence、ledger Unresolved>=1；
   - coverage 非法输入 fail-closed、合法输入不改变五项计数；
   - classifier 未配置（null 委托）路径行为不变；
4. 验收通过后回填 tasks.md 3.2/5.1/6.4 证据；不通过则退回 Worker 或按 §10 内联执行。
