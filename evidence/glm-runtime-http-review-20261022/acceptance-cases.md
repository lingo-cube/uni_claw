# Runtime HTTP 现有公开接口验收用例（正/负）

> Date: 2026-10-22
> 关联：`review.md`（同目录）。用例针对**现有** HTTP 契约设计，用于回归守护；本次未执行（禁跑设备/模型/服务）。
> 每条标注所守护的语义：〔Owner〕身份铸造 〔幂等〕〔拒绝〕〔零副作用〕〔终态〕。
> 前置：base URL `http://127.0.0.1:<port>/api/uniclaw-runtime`；错误 envelope 字段 `ok=false, error{code,message,retryable}`；成功 envelope 带 `schemaVersion/contractVersion`。

## A. 正向用例

| # | 操作 | 期望 | 守护 |
|---|---|---|---|
| A1 | `POST /tasks` `{taskRef, idempotencyKey:K}` | 200；返回**新铸造** taskInstanceId，productSessionId=null；同 K 重发 → 同一实例返回 | Owner·幂等 |
| A2 | `POST /sessions` `{taskInstanceId}` | 200；1:1 铸造 productSessionId 并回写绑定；对同 taskInstance 重发 → 同一 session | Owner·幂等 |
| A3 | `POST /runs` 全字段（含 taskInstanceId/productSessionId/acceptedContractViewRef/contractGeneration=1，**外加伪造 runId**） | 202 Accepted + `Location: /runs/{id}`；响应 run.runId ≠ 请求 runId（调用方 ID 永不成为 canonical） | Owner |
| A4 | 对已 finalized 的 activation 重放同一 launchId/idempotencyKey 的 `POST /runs` | 202 + `idempotent:true` + 同 runId 投影；不新建 run、不触发第二次 DSH attach | 幂等 |
| A5 | `GET /runs/{id}` / `GET /runs?status=&cursor=` / `GET /runs/{id}/events?cursor=&source=` | 200；投影字段、nextCursor 分页、revision/observedAt 单调 | 投影 |
| A6 | `GET /tools` | 200；workbench surface 工具清单 + output{base,subdir}；只读投影 | 暴露面 |
| A7 | `POST /tools/run-report/invoke` `{runDir}`（该 run 已完成） | 200；exitCode/stdoutTail/reportJson/reportMd 引用存在 | 产物 |
| A8 | `POST /runs/recover` `{idempotencyKey}` 或 `{launchId}` | 命中 → 200 现投影；未命中 → 404 `runtime-run-not-found` | 幂等·拒绝 |
| A9 | `POST /runs` 同 activation 键 + `resume:true`（此前 reserved） | 走创建路径而非 409 | 幂等 |

## B. 负向用例

| # | 操作 | 期望 | 守护 |
|---|---|---|---|
| N1 | `POST /tasks` 缺 taskRef 或 idempotencyKey | 400 `invalid-request` retryable=false；task-instances.jsonl **无新行** | 拒绝·零副作用 |
| N2 | `POST /sessions` 未知 taskInstanceId | 404 `task-instance-not-found`；sessions 文件无写 | 拒绝·零副作用 |
| N3 | `POST /runs` taskInstance 与 productSession 交叉错配 | 409 `activation-binding-mismatch` | Owner·拒绝 |
| N4 | `POST /runs` 未知 taskInstance/sessionId | 404 `activation-binding-not-found` | 拒绝 |
| N5 | `POST /runs` 同 launchId 换不同 activation 键 | 409 `activation-key-conflict` | 幂等·拒绝 |
| N6 | `POST /runs` session 已绑定其他 Run | 409 `product-session-already-bound`；**且** activation 记录被 Fail("blocked","product-session-already-bound")——此有限写为既有语义，必须断言 | 拒绝（有限副作用） |
| N7 | `POST /runs` activation 处 failed/blocked | 409 `activation-closed` retryable=false | 拒绝 |
| N8 | `POST /runs` activation reserved 且未带 resume | 409 `activation-reserved` **retryable=true** | 拒绝 |
| N9 | `GET /runs?cursor=垃圾` / `GET /runs/{id}/events?cursor=垃圾` | 400 `invalid-cursor`；events 对未知 runId → 404 | 拒绝 |
| N10 | `POST /tools/{name}/invoke`：缺 runDir→400；未知工具→404 `tool-not-found`；runDir 含 `../` 或路径分隔符→400 `invalid-run-dir`；registry 文件缺失→503 `tool-registry-missing` | 拒绝·逃逸防护 |
| N11 | （P4 缺口回归）在 store Transition 注入故障（见下） | 期望行为：revoke+dispose 已 attach 会话、run 转 failed、返回 Error envelope——**现状走默认 500 且泄漏**，此用例当前应 RED | 资源生命周期 |

## C. 说明

- N6/N8 的 retryable 位与有限副作用写序是**现行契约**，拆分（review.md S2 ActivationGate）后逐位保持不变。
- N11 无法经公开接口注入故障（store 为进程内 jsonl），这正是 review.md §5 以内部缝（internal seam）承接身份/激活/终结逻辑的测试性论证：缝不是为抽象而抽象，而是让上表全部语义可脱离 HTTP+DSH+adb 断言。
- A7 依赖外部 python 工具与文件系统产物，属 ENVIRONMENT 级；其余为 SCENARIO 级（uniflow A6.5 验证等级）。
