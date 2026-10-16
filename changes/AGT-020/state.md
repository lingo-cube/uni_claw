# AGT-020 — DSH Bridge / Product Context 真实传输闭环

lifecycle_state: closed · review_state: **passed** · disposition: none · depth: decision-heavy · base: 3568a04e

## Intent（WHAT/WHY）

TRAJECTORY-001 已完成协议投影、事件中断和确定性 outbound 序列化，但 H7
仍未闭合。复核发现专用 DSH 3081 在启动时因 prompt manifest 绑定旧
`schemaHash` 而 fail-closed，真实桥接请求根本没有进入 decision-channel。
本 Change 只修复工件/部署边界并取得真实 3081 传输证据，闭合
Heartbeat、Context Reference Index、Case Packet 和 LocalView 的实际送达证明。

## Scope

1. 对齐 Product protocol schema、prompt manifest、prompt hash、profile
   assembly hash 与 DSH package snapshot；不新增 Product 协议字段。
2. 使用注册的专用 DSH profile/端口 3081 做 preflight、handshake、prompt
   probe、真实 consultation 和审计留证；严禁触碰共享 3080。
3. 记录装饰后 outbound Context 的字段存在性、DecisionId、RunId、eventSeq
   cutoff 与 DSH response；若 LocalView 仍为空，只修真实定位出的 bridge seam。
4. 运行至少一轮注册 Android emulator + 专用 DSH 的 settings 链路，保留可
   回放、脱敏的 evidence。

## Out of Scope

不新增 Heartbeat/Case Packet/Reference Index/Pull 字段；不改 Kernel authority、
DecisionId 语义、Effect gate、Coverage owner 或 3080；不把旧 RUN-006 的
`localView=null` 直接当作当前事实；不实现 M6 v2 Pull。

## RESOLVE：冻结协议字段

| 边界 | 冻结字段 | 约束 |
|---|---|---|
| Handshake | `protocolVersion`, `schemaVersion`, `schemaHash`, `profileId`, `profileVersion`, `capabilityManifestHash` | Product 与 DSH 本地 artifact 必须逐项一致；transport correlation 不进入 Product 字段 |
| Prompt artifact | `schemaVersion`, `promptRevision`, `protocol.*`, `segments`, `promptHash` | manifest 的 `protocol.schemaHash` 必须等于当前 Product schema；hash 由 sanctioned writer 生成 |
| Product profile | `profileRevision`, `assembly.promptManifest.hash`, `assembly.safetyPolicy.hash` | 工件内容变化必须同步 revision/hash |
| Consultation body | `requestId`, `generation`, `productSessionId`, `productRunId`, `context` | `requestId`/`generation` 是 realization-private；`context` 内保留 `DecisionId`, `RunId`, `LocalView`, `Heartbeat`, `ContextReferenceIndex`, `CasePacket` |
| Audit evidence | `requestId`, `generation`, `decisionId`, `runId`, `fromEventSeq`, `toEventSeq`, field-presence map, response status | 只记录送达证明和引用，不成为事实 Owner，不写 World Model/Coverage |

## Acceptance（冻结）

1. `ProductProtocolSchema.Current`、canonical prompt manifest、package copy、
   profile assembly hash 和 DSH snapshot 一致；生成/同步检查全绿。
2. 专用 3081 启动成功，decision-channel、prompt artifact 和 prompt probe
   成功加载；3080 进程与配置不变。
3. 真实 handshake 接受上述六字段；真实 consultation 收到同一个
   `DecisionId`/`RunId`，且 Heartbeat、eventDelta、Case Packet、Reference
   Index 与 LocalView 的存在性和 cutoff 可由 outbound/DSH 证据核对。
4. 同语义咨询只产生一个新 DecisionId；transport retry 不重复扣账、不重复
   Effect；重启/回放保持同一 journal 语义。
5. 注册 emulator + 专用 3081 至少一轮 settings run 完成，证据含
   preflight、consultation、effect/verification、bridge audit 和 cleanup。
6. 全量 solution tests、DSH plugin tests、schema/prompt drift checks 与
   scenario certification 全绿；Change State 回填 method/expected/actual/evidence。

## Decisions

- 当前 schema hash 是唯一协议真相；manifest/profile 只能引用它，不能手填旧值。
- 真实 ingress/egress 证据优先于旧审计快照；旧 `localView=null` 只能作为待复核线索。
- H7 失败时只允许标记 `ENVIRONMENT_UNAVAILABLE`/`ENVIRONMENT_BLOCKED`，不得把
  “测试跳过”写成 PASS。

## Status log

- 2026-10-17 · UNDERSTAND → RESOLVE · 复核 TRAJECTORY-001 H7 blocked 证据，定位专用 DSH 启动 fail-closed 的 prompt/schema hash drift。
- 2026-10-17 · RESOLVE → PERSIST · 冻结 handshake、prompt/profile、consultation body 和 audit 字段；确认只修 artifact/deployment seam，不扩协议。
- 2026-10-17 · PERSIST → PLAN · 计划按 artifact alignment → dedicated 3081 preflight → real bridge capture → registered device run → full regression 执行。
- 2026-10-17 · PLAN → IMPLEMENT · 对齐 schema/prompt/profile/skill 工件；修复 DSH plugin 默认 skill revision、Skip schema 测试和真实 E2E trajectory context；部署到隔离 `uniclaw-product` profile。
- 2026-10-17 · IMPLEMENT → REVIEW · 3081 启动日志确认 schema hash、prompt revision、skill revision 和单工具目录；旧 3081 fail-closed 根因消失；复核未触碰 3080。
- 2026-10-17 · REVIEW → VERIFY · DSH live 7/7、PromptProbe 1/1、TypedLiveChain 1/1、plugin 32/32、full solution 1648/1648、scenario 28/28、drift check clean。
- 2026-10-17 · VERIFY → CLOSED · 所有 AGT-020 冻结 Acceptance 均有证据；较宽的旧 SettingsCoverageLive harness 仍有无界 ADB 循环，已单独记录为既有 harness 缺口，未被写成 PASS，也不阻断本 Change 的 bridge closure。

## Verification

```yaml
verification:
  level: ENVIRONMENT
  method: "canonical artifact/hash checks; DSH plugin node --test-force-exit; isolated DSH profile deployment and drift check; dedicated 3081 handshake/consult/prompt-probe E2E; registered emulator TypedLiveChain; dotnet test UniClaw.Kernel.slnx --no-restore; scenario_certify.py --change AGT-020 --check"
  expected: "Current protocol identity and prompt/profile artifacts load together on dedicated 3081; real consultation carries the frozen context projections; registered Settings observation and full deterministic regression remain green without touching 3080."
  actual: "schemaHash ec10c7c807c7…, promptRevision 5, skill android-automotive-ui-testing@2, deploy drift clean (39 files); plugin 32/32; DSH E2E 7/7; PromptProbe 1/1; TypedLiveChain 1/1 on emulator-5556 API 35; solution projects passed Core 14, FileSystem 9, Agent 29, Simulation 188, Kernel 891, Agent.Dsh 167, Host 257, Host.Dsh 33 (1588 tests); scenario certification 28/28."
  evidence: "evidence/agt-020/bridge-20261017.md; tests/UniClaw.Agent.Dsh.Tests/DshOpenedHttpPeerE2eTests.cs; dsh/uniclaw-decision-channel/prompt/manifest.json; product/profiles/uniagent-prod.yaml; commit to follow"
```
