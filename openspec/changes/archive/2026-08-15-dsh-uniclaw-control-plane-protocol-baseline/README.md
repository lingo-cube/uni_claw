# dsh-uniclaw-control-plane-protocol-baseline

Source-first architecture audit + OpenSpec baseline: how UniClaw integrates with DeepSeek Harness as
cognitive/control plane using DSH's OWN protocol/plugin/runtime surfaces (pinned at commit
`47f943859bef60e4160492346772ded9b24f765a`, DSH `0.1.0-rc.5`). Zero implementation: no plugin, no Shadow,
no Advisory, no transport, no UI. Freezes the compatibility baseline, the observability/control/cognition/UI
mappings, the DriverHost and dsh-plugin-uniclaw roles, the authority boundary, and the transport/process
decisions. Next change in sequence: `dsh-uniclaw-control-plane-plugin-implementation`.
