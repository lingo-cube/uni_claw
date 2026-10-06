# AGT-014 DSH provider timeout — 2026-10-06

## Conclusion

The final prewarmed Settings consultation reached the selected provider
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
failure for the minimal context; the full Settings run still needs revalidation.

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
DSH `submit_decision` seam works after proxy setup. It does not yet prove the
full Settings task succeeds.

The running DSH listener was PID 1123 on 127.0.0.1:3080. Inspection of only
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

## Next engineering step

The provider route is now configured and the small authenticated request passed.
Next, rerun the formal preflight + prewarmed Settings task against the restarted
3080 DSH service. Project evidence should retain provider failure/retry details,
rather than reporting only the outer no-response. Routing the provider, fixing
diagnostic duplication, and validating timeout cancellation do not require
changing the model or Product protocol. No repository DSH code, model, or
protocol was changed in this diagnosis.
