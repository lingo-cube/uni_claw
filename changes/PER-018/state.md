# PER-018 — Real Slow Perception Providers
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

在 PER-017 Slow orchestration 冻结边界内接入两个真实 OpenCode realization：

```text
slow.semantic.text   → DSH opencode-go / deepseek-v4.1-flash
slow.semantic.visual → DSH opencode-go / deepseek-v4-flash-vision-exp
```

真实 provider 只负责请求序列化、调用、schema 校验和 `SlowResult`；所有 proposal
仍通过现有 P2 → Evidence Ledger → WorldModel 链路。两个 profile 只能保持
`experimental` 或 `replay-validated candidate`。

## Scope

- OpenCode model catalogue availability audit 与启动时冻结 binding snapshot。
- 一个共享真实 Slow adapter，支持 Product-normalized text context 与同 capture PNG。
- 429/529 容量的 invocation-local 延迟重试；timeout/cancel/unavailable/schema/malformed
  fail-closed；Partial 只保留合法 proposal。
- provider/model identity provenance 与 invocation telemetry。
- Text/Vision controlled adapter probes 和既有 P2 ingress。

## Out of Scope

- 修改 PER-017 request/result、P2/P3、WorldModel、Fusion 或 Effect authority。
- Text↔Vision 自动 fallback、confidence/reliability/voting、Settings Traversal。
- production eligibility 或 paired benchmark。
- provider 直接写 Evidence Ledger/WorldModel/Effect。

## Decisions

1. Concrete model identity 只属于 realization binding；Product 只接收 logical profile 和
   冻结 snapshot。
2. 配置字符串不是 availability 证据；catalogue probe 未确认时为 `ROUTING_UNAVAILABLE`。
3. Capacity retry 不生成新的 semantic invocation，也不创建新的 AttemptKey。
4. Vision 只使用同一 `RawArtifactRef` 解析的 PNG；`ArtifactId` 与 `CaptureId` 不混用。
5. Provider response 必须是结构化 JSON；自由文本不能升级为 ObservationProposal。

## Acceptance

1. Text/Visual profile binding exact identity is retained in snapshot and provenance.
2. Text context serialization is deterministic and contains only typed bounded context.
3. Vision body contains resolved PNG while preserving independent ArtifactId/CaptureId.
4. Model audit, delayed capacity retry, failure statuses, malformed/free prose and Partial
   filtering are deterministic and fail closed.
5. Both adapter probe outputs enter the existing P2 projector in tests; no direct authority path.
6. Both profiles remain `Experimental`; no production-ready claim is made.

## Constraints

- Preserve all pre-existing PER-017 dirty files and semantics.
- DSH 3080 is the provider boundary; reuse its credential-backed capability and never copy its API key into Product configuration.
- Do not treat DSH catalogue declaration as runtime semantic success.
- Live execution must stop at environment failure and be reported honestly.

## Verification

```yaml
level: ENVIRONMENT
method: focused OpenCode adapter tests; DSH plugin tests; exact DSH text/vision probes through authenticated port 3080; `dotnet build UniClaw.Kernel.slnx`; `dotnet test UniClaw.Kernel.slnx`; `python3 tools/scenario_certify.py --check`; elevated `tools/verify-live`
expected: adapter/P2 tests pass; DSH exact text route returns a Product-valid response; vision route accepts same-capture PNG and reports structured-output status; solution builds/tests/certification/live baseline pass
actual: OpenCode adapter tests 9/9; DSH plugin tests 13/13; exact `deepseek-v4.1-flash` DSH consult PASS; exact `deepseek-v4.1-flash` DSH Slow JSON PASS; exact `deepseek-v4-flash-vision-exp` DSH Slow JSON with same-capture PNG PASS after the dedicated no-tool Slow preset; full solution build 0 errors, full solution tests 1096/1096, certification 29/29, verify-live baseline PASS
evidence: tests/UniClaw.Kernel.Tests/Perception/OpenCodeSlowRealizationTests.cs; tests/UniClaw.Agent.Dsh.Tests/DshOpenedHttpPeerE2eTests.cs; dsh/uniclaw-decision-channel/tests/plugin.test.mjs; evidence/PER-018-binding-audit.md
```

## Status log

- 2026-09-28 · persisted→planned · PER-018 slices mapped to PER-017 frozen seams.
- 2026-09-28 · planned→implemented · Added OpenCode adapter, model audit, capacity retry,
  structured parsing, provenance, P2 probe tests and DSH concrete profile declarations.
- 2026-09-28 · implemented · Direct OpenCode CLI catalogue was corrected to the DSH 3080 provider surface; exact text and vision Slow routes now return structured JSON.
- 2026-09-28 · implementation completed · Dedicated `uniclaw-slow` no-tool preset prevents `submit_decision` schema leakage into Slow provider calls; direct Product authority remains unchanged.
- 2026-09-28 · expectationsDigest unchanged; 29 scenarios re-certified for PER-018; full solution 1096/1096 tests pass after the DSH image seam and Slow route changes.

## Residual risks / Gate

- The standalone OpenCode CLI catalogue is not the 3080 DSH catalogue; both exact profiles are now live-verified through DSH Slow routes.
- Profiles remain experimental and still require normal P2 admission; no production eligibility claim is made.
- No Settings Traversal is started by this change.
- 2026-10-02 · implemented→closed · 复验关门 + owner 指令换绑：
  ①slow.semantic.text 由 opencode-go/deepseek-v4.1-flash 换绑
  zai-coding-cn/glm-5.3-flash（起因：deepseek-v4.1-flash 在专用实例
  当轮不调 submit_decision/no-slow-result；根因确证为 opencode-go 上游
  403 订阅失效）；换绑落点 model-bindings.yaml + adapter 测试镜像断言 +
  e2e 映射 glm53Flash。②新鲜四元组：确定性面全量 1228/1228、Slow 聚焦
  42/42、插件 21/21、认证 29/29；ENVIRONMENT 面在专用 3081 重验——
  text profile（新绑定 glm-5.3-flash）e2e 7/7 含 slow 结构化 JSON PASS；
  visual profile（opencode-go/deepseek-v4-flash-vision-exp）当前被上游
  订阅 403 阻断（如实记录，非代码回归；2026-09-28 经 3080 的原始通过
  证据仍在本 Verification）。两 profile 维持 experimental。visual profile
  的上游恢复或换绑（如 zai 视觉模型）由 owner 另行裁决。

