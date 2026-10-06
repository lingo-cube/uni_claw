# AGT-014 DSH provider timeout — 2026-10-06

## Conclusion

The initial prewarmed Settings consultation reached the selected provider
(`zai-coding-cn/glm-5.3-flash`), but every provider attempt failed with `TIMEOUT`.
The local DSH process had no proxy environment configured. A fresh unauthenticated
Node fetch to the same provider endpoint reproduced `UND_ERR_CONNECT_TIMEOUT`
after 10.551s. The same endpoint through proxy 7890 returned HTTP 401 in 0.159s.
401 establishes network reachability only; this probe did not test credentials,
model availability, or successful decision submission.

This local transport failure explains the repeated provider TIMEOUTs. After the
DSH home proxy was configured, a dedicated authenticated smoke consultation
using the same provider/model captured an `act` decision in 11.177s. The
outer 75s consultation deadline explains the final Product `no-response`, but
is not the initiating failure. This also rules out a general protocol or prompt
failure for the minimal context; the full Settings run was revalidated below.

## Readable session evidence

Final run: `dsh-task2-prewarm-final/run-20261006-013701-765/`.

DSH source journal (read-only extraction, no secrets copied):
`/Users/fran/.dsh/sessions/--Users-fran-.dsh-uniagent-workspaces-UniClaw_Product_Tasks--/session-4f93f080-382a-4979-88a0-3227861ced22/session.v4.jsonl.zstd`.

Times below are relative to DSH session creation, not formal perception startup.

| Time (s) | Event | Observed result |
|---:|---|---|
| 0.078 | model/selection | zai-coding-cn/glm-5.3-flash |
| 0.250 | request/context | selected provider/model confirmed |
| 10.843 | assistant/attempt | TIMEOUT, Request timed out. |
| 11.322 | retry 1 starts | next attempt |
| 21.831 | assistant/attempt | TIMEOUT |
| 22.858 | retry 2 starts | next attempt |
| 33.371 | assistant/attempt | TIMEOUT |
| 35.569 | retry 3 starts | next attempt |
| 46.080 | assistant/attempt | TIMEOUT |
| 49.929 | retry 4 starts | next attempt |
| 60.440 | assistant/attempt | TIMEOUT |
| 68.585 | retry 5 starts | next attempt |
| ~75 | channel consultation deadline | decision=null, turn-timeout |
| 79.094 | final assistant/attempt | TIMEOUT |
| 79.097 | turn/end | error/TIMEOUT |

All six attempts record zero reported input/output token usage, no text chunks,
and no tool call. Zero reported usage is not proof that the remote server never
received a request. Two earlier independent sessions (`session-feb660b5-1036-466b-b6c8-9523d773a3a2`
and `session-ad4f6c06-5b7e-4489-b186-6879cceb18bd`) show the same initial attempt
+ five retries + TIMEOUT pattern.

The final consultation prompt text was 6,445 UTF-8 bytes and contained 8 Elements,
4 CurrentWorldClaims, and 1 PendingObligation. No image block was sent. A separate
499-byte text message and the system/tool definitions were also present; 6,445
bytes is not the total model request size. The 586 perception proposals were
not sent as 586 Agent context elements. The earlier context-pressure hypothesis
has no support in this run.

## Current network reproduction

No credentials were supplied in these probes.

| Method | Expected | Actual |
|---|---|---|
| Direct curl, 12s connect budget | reach provider HTTP endpoint | rc=28, DNS resolution timeout, HTTP 000 |
| Direct Node fetch, 15s overall signal | reach provider HTTP endpoint | 10.551s, ConnectTimeoutError/UND_ERR_CONNECT_TIMEOUT, attempted open.bigmodel.cn:443, connection budget 10000ms |
| curl through http://127.0.0.1:7890 | reach provider HTTP endpoint | HTTP 401, TLS 0.066s, total 0.159s |

## Proxy-fixed smoke verification

The DSH user configuration now contains `HTTP_PROXY` and `HTTPS_PROXY` pointing
to `http://127.0.0.1:7890` (the values are outside the repository). A dedicated
DSH instance on port 3081 was started after that configuration was loaded. The
same live E2E test and same `zai-coding-cn/glm-5.3-flash` selection then produced:

| Method | Expected | Actual |
|---|---|---|
| `E2E_Consult_ChannelRoundTrips_FailsClosedWithoutInventedDecisions` against dedicated 3081 | handshake + consult reaches the model and returns a typed decision or an explicit fail-closed result | PASS in 12.172s; DSH logged `consult captured`, `kind=act`, `durationMs=11177` |

This smoke run used the bounded test context and did not touch Android or dispatch
an effect. It proves the provider route is usable through the proxy and that the
DSH `submit_decision` seam works after proxy setup. The later full Settings run
also succeeded after the runtime-token prompt/projection fix described below.

The earlier DSH listener was PID 1123 on 127.0.0.1:3080. Inspection of only
the proxy-related process environment found HTTP_PROXY, HTTPS_PROXY, ALL_PROXY,
their lowercase variants, and NODE_USE_ENV_PROXY unset. Git's separately scoped
7890 proxy does not configure this process.

## Source locations and diagnostic gaps

- `src/UniClaw.Agent.Dsh/DshOpenedHttpPeer.cs`: sends turnTimeoutMs=75000.
- `dsh/uniclaw-decision-channel/src/index.js`: starts the consultation deadline,
  forwards the prompt, and waits for submit_decision.
- `src/UniClaw.Host.Dsh/Program.cs`: outer adapter budget=110s.
- `src/UniClaw.Kernel/Runtime/KernelRunDriver.cs`: derives the bounded Agent
  context and removes raw ui.node.* world claims.
- `src/UniClaw.Agent.Dsh/DshAgentAdapter.cs`: null response calls both
  AddDiagnosticLocked and RetireActiveLocked, producing duplicate no-decision
  diagnostic lines for one transport response. Two printed lines do not prove
  two consultations. The final session had one turn and one Product consult.
- DSH retries continued to ~79s after the channel's ~75s deadline. This is a
  cancellation/lifecycle observation to verify when preparing the next run.

## Runtime-token 分歧与修复

7890 代理后的首次正式 Settings 回合不再卡在 provider timeout，而是返回了合法
AgentDecision；模型把策略语义 `navigate` 放进了 `effectClass`。本轮
`context.allowedEffects` 只有 `tap`，所以 Host Guard 将它分类为
`unknown-action` 并在 dispatch 前拒绝，设备没有被触碰。

修复在 DSH prompt 和 Host policy projection 中同时声明：`effectClass` 必须逐字
复制 `context.allowedEffects`；Settings 的 `tap` 对应导航/返回，`swipe-up`
对应滚动。没有放宽 Runtime allowed effects，也没有修改模型、协议或策略文件。

修复后的正式运行目录：
`evidence/agt-014/dsh-task2-proxy-runtime-token-fix-20261006/run-20261006-020702-678/`

- DSH 三次咨询：`tap Network & internet`、`tap Internet`、`noAction Wi-Fi=checked`。
- facts：`Completed / Completion / delivered=2`；两个 Guard 均 `Allow / navigate`。
- exec journal：两个 `DeliveryCompleted`，没有 Wi-Fi 开关点击。
- Node plugin 全量 22/22 PASS；SettingsActionPolicyTests 8/8 PASS；Host.Dsh 构建 0 errors。

## Next engineering step

The provider route and the formal prewarmed Settings task are now verified. Keep
the provider retry evidence alongside the successful run rather than reporting
only the old outer no-response. The remaining AGT-014 work is the other real
tasks and the fault-injection/observability summary; no model or Product protocol
change is required by this diagnosis.
