# Design — runtime-debug-p1d-packet-generator

## Context

P1a/P1b/P1c built a packet-consumption core (summarize/occurrence/causal/diff) and a second source adapter (capture bundle → AssetRef index). P1d closes the loop: a base Evidence Packet generator so real capture bundles can enter the analysis chain without hand-authoring packets. The generated packet must satisfy the P1a reader contracts (packetVersion prefix, unique evidenceIndex refIds, resolvable EvidenceRefs, repairGate.eligible boolean) so it round-trips through the existing commands.

## Goals / Non-Goals

Goals: stored-facts-only base packet from one bundle; deterministic; round-trip proven; AssetRef→EvidenceRef binding; explicit target selection.

Non-Goals: semantic chain construction (needs normalized/fused/canonical knowledge absent from bundles); FDP/Owner/Disposition inference; multi-run pair generation; writing packet files (stdout only); harness changes.

## Decisions

### D1 — Generator lives in Query Core, bundle is the only source
**Decision:** `query.generate_packet(bundle, case_id, target_seq)` consumes the same `CaptureBundle` model as the asset commands; the CLI adds one thin command. No new logic anywhere else.
**Why:** keeps the "one Core" rule and reuses the checksum-verified bundle adapter.

### D2 — Absent semantics are declared MissingEvidence, never fabricated
**Decision:** every semantic facet a raw bundle cannot supply (ExpectedReality, occurrence identity, Good/Bad, stage chain) becomes a MissingEvidence entry; the corresponding IR fields are simply absent from the generated packet.
**Why:** the gate's FACT/INFERENCE/MISSING discipline — the packet advertises what it lacks.

### D3 — Assets become evidence refs
**Decision:** each bundle artifact becomes a `CAPTURE_ASSET` evidenceIndex entry carrying AssetRef facts (relative uri, sha256 digest, mediaType, selector with observationSeq/frameId). The target occurrence's evidenceRefs point at the target frame's assets.
**Why:** closes the Foundation promise "EvidenceRef → AssetRef" mechanically, with zero upstream labelling.

### D4 — Target observation is explicit or final-recorded
**Decision:** `--observation-seq` selects a specific recorded observation; absent, the final recorded Observation (stored record order) is used. Unknown explicit seq → `EVIDENCE_UNAVAILABLE`.
**Why:** explicit-input contract; the "final recorded observation of THIS bundle" is a stored fact, not a guessed `latest`.

## Risks / Trade-offs

- [Generated target is UNRESOLVED/CANDIDATE, not a diagnosis] → by design: the generator produces the packet skeleton; Agent/Skill performs semantic diagnosis on top (Foundation analysis contract).
- [EvidenceChain absent → `trace` command returns no stages] → honest; the missing facet is declared in MissingEvidence; a future labelled/normalized source can populate the chain.

## Migration Plan

None — additive generator; no schema/wire/Runtime change; generated packets are new files the consumer side already understands.

## Open Questions

None that would change the contracts; pairing runs for Good/Bad generation and chain construction are separate later slices.