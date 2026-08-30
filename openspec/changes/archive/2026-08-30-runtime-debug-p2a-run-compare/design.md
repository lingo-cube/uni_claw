# Design — runtime-debug-p2a-run-compare

## Context

P1c bundle adapter + P1d generator established the bundle model and AssetRef index; the umbrella CLI contract names `run compare`. P2a adds the paired-bundle structural diff reusing only stored facts.

## Goals / Non-Goals

Goals: three-axis UNCHANGED/CHANGED diff, asset add/remove/hash-change list, deterministic digests, fail-closed pairing.

Non-Goals: semantic first-divergence inference (needs semantics — Agent); cross-packet compare (would need normalized alignment); timing compare (bundles lack timestamps); chain construction.

## Decisions

### D1 — Axis semantics are stored-facts-only
**Decision:** terminal compares manifest-stored facts; records compares counts/last observation seq; assets align by ArtifactId with sha256 equality. The output carries an explicit note that FIRST_SEMANTICALLY_RELEVANT is not inferred.
**Why:** structural facts first (analysis contract); no inference masquerading as fact.

### D2 — One CLI command over the same bundle adapter
**Decision:** `run-compare <good> <bad>` routes through `_bundle_command` (same Query Core, same checksum-verified reader, same envelope).
**Why:** keeps one Core and one source adapter; zero new IO surface.

## Risks / Trade-offs

- [Asset alignment by id only] → documented; content-addressed rename would count as removed+added (honest at this fidelity).

## Migration Plan

None — additive command.

## Open Questions

None that would change the contract; semantic first-change inference and timing compare await labelled/normalized sources.
