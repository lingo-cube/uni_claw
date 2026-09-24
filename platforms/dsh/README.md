# AGT-002 Product DSH sidecar

`product-sidecar.mjs` is the headless stdio JSON-RPC skeleton used by the .NET
`JsonRpcStdioTransport`. Its runtime capability manifest is exactly
`submit_decision`; it has no shell, filesystem, browser, device, workflow,
subagent, approval, or user-interaction tool.

The sidecar accepts `initialize`, `consult`, and `abort_current_turn` transport
methods. `consult` may read a replay map supplied by `DSH_REPLAY_FILE`; without a
provider it returns a fail-closed provider error. `DSH_SESSION_ID` and
`UNICLAW_SCHEMA_HASH` are realization configuration, never Product authority.

The checked-in schema artifact is generated from the .NET records with
`ProductProtocolSchemaGenerator.WriteArtifact`, then consumed by the sidecar as
the handshake hash. Do not edit the artifact or add a second handwritten union.
