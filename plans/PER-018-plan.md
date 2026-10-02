# PER-018 Plan — Real Slow perception providers

> WHAT/WHY/Acceptance 以 `changes/PER-018/state.md` 为准。本计划只描述真实
> realization 的实现与验证顺序，保持 PER-017 request/result 与 P2 authority 不变。

## Slice A — binding and audit

1. 在 DSH adapter binding 文件声明两个 concrete profile：`opencode-go/deepseek-v4.1-flash`
   与 `opencode-go/deepseek-v4-flash-vision-exp`。
2. 启动时由 DSH provider catalogue/live probe 证明 model id；未被 DSH 宣布的
   binding 生成 `ROUTING_UNAVAILABLE`，不进入调用。
3. 将 probe 结果转为冻结 `ModelBindingSnapshot`，profile 继续 `experimental`。

## Slice B — shared real adapter

1. 复用 PER-017 `SlowPerceptionRequest` / `SlowResult`。
2. Text 请求只序列化 typed Product context；Vision 通过 `RawArtifactRef.ArtifactId`
   解析同一 capture 的 PNG，并经 DSH 原生 image content 传入 provider。
   DSH Slow 调用使用独立 `uniclaw-slow` no-tool preset，避免 Product
   `submit_decision` schema 进入 provider 请求。
3. 仅接受结构化 `SlowResult` JSON；free prose、坏 schema、坏 proposal 均 fail closed。
4. provider/model identity 留在 binding snapshot、provenance lineage 和 invocation telemetry。

## Slice C — failure and capacity

- 429/529 在同一 semantic invocation 内按有界延迟重试；耗尽后
  `InfrastructureFailure` 且零 proposal。
- timeout/cancel/unavailable/schema/malformed/invalid-input 均零 proposal。
- Partial 逐条过滤合法 proposal；不生成 fabricated evidence。

## Slice D — P2 and probes

- Text/Vision real adapter output 只经 `SlowResultProjector` 进入 P2，再由既有
  Evidence Ledger/WorldModel 接管。
- deterministic tests 覆盖 model audit、context serialization、artifact/capture 双标识、
  delayed capacity retry、malformed/free prose、P2 ingress。
- live DSH and `verify-live` remain an environment gate; no production eligibility claim.
