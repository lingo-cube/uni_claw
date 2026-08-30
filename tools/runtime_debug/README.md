# runtime_debug — read-only Runtime debugging projections

Offline, deterministic, **Python-standard-library-only** projection of complete
P0 Evidence Packet v0 files and byte/digest-verified Harness capture bundles.
The implemented command surface covers the archived P1a–P1d and P2a–P2c
slices, with post-graduation conformance repair applied. No Runtime / DriverHost
/ device / network access; no writes; no authority.

## Interfaces

```
tools/runtime-debug summarize <packet>
tools/runtime-debug occurrence <packet> --occurrence-id <value>
tools/runtime-debug occurrence <packet> --stable-key <value>
tools/runtime-debug occurrence <packet> --row-id <value>
tools/runtime-debug occurrence <packet> --evidence-ref <value>
tools/runtime-debug trace <packet> [--prune stage,...] [--only-decisions] [--only-evidence]
tools/runtime-debug evidence <packet> --evidence-ref <refId>
tools/runtime-debug diff <packet>
tools/runtime-debug assets <bundle-dir>
tools/runtime-debug execution-tree <bundle-dir> [--hide-layer/component/name ...] [--only-errors] [--time-from NS --time-to NS]
tools/runtime-debug asset-show <bundle-dir> --asset-id <assetId>
tools/runtime-debug asset-related <bundle-dir> --asset-id <assetId>
tools/runtime-debug packet-generate <bundle-dir> --case-id <name> [--observation-seq <N>]
tools/runtime-debug run-compare <good-bundle> <bad-bundle>
tools/runtime-debug trace-diff <good-packet> <bad-packet>
tools/runtime-debug terminal-chain <packet>
```

Every source is explicit. Commands accept one packet, one bundle, or the
contract-named Good/Bad pair. No globs, run-id shortcuts, repository discovery,
or implicit `latest`.

## Non-interfaces

- Packet EvidenceRef URIs are metadata-only and are never dereferenced. The
  capture-bundle adapter streams declared artifact bytes only to verify
  byteCount and SHA-256; it never decodes, copies, inlines, or mutates them.
- No direct standalone stage-artifact, log, or live-Runtime reading.
- No FDP / root cause / Owner / Disposition / repair-eligibility computation;
  terminal-chain only labels already stored diagnostic fields as `STORED`.
- No semantic first-divergence inference, automatic Owner selection, repair,
  replay, or minimization.

## Result envelope

One JSON object on stdout:

```json
{ "contractVersion": "runtime-debug-cli.p1a", "command": ...,
  "status": ..., "source": {packetVersion, packetId, sourceIdentity},
  "result": ..., "diagnostics": [{code, message, evidenceRefs}] }
```

UTF-8, sorted keys, fixed separators, single trailing newline. No timestamps,
absolute paths, pids, or stack traces. Diagnostics are stably sorted.

## Closed status → exit code

| status | exit |
|---|---|
| OK | 0 |
| INVALID_INPUT | 2 |
| EVIDENCE_UNAVAILABLE | 3 |
| IDENTITY_MISMATCH | 4 |
| AMBIGUOUS_OCCURRENCE | 5 |
| INSUFFICIENT_TRACE_COVERAGE | 6 |
| SCHEMA_VIOLATION | 7 |

## Layering (职责清晰 / 可扩展 / 可替换)

| module | responsibility |
|---|---|
| `status.py` | closed status vocabulary + exit mapping (single source of truth) |
| `envelope.py` | canonical result envelope (deterministic serialization) |
| `packet.py` | **source adapter A** — complete P0 packet v0 shape/closed-vocabulary/reference-closure validation; never dereferences EvidenceRef URIs |
| `sources/bundle.py` | **source adapter B** — actual camelCase Harness bundle → verified AssetRef index; validates publication, records, safe paths/relations, checksum coverage, byteCount and streamed SHA-256 |
| `query.py` | **Query Core** — pure deterministic stored-fact projections and complete explicit-absence packet generation |
| `cli.py` | thin argv adapter only |

Extension seams:

- New source (any future input) → implement a module producing the model(s)
  consumed by `query.py` next to `packet.py` / `sources/bundle.py`; the Query Core
  and CLI stay unchanged.
- New commands/rules → add to `query.py`; CLI only registers flags.
- TUI (future) → calls `query.py` functions directly; never reimplements logic.
- Asset semantic labels (screenshot / crop / overlay …) are stored facts provided
  by producers; the index layer never guesses them (`assetType` = stored
  ContentType or `capture.artifact`).

## Authority boundary

READ_ONLY · DETERMINISTIC · NO_RUNTIME_AUTHORITY · NO_TRACE_MUTATION.
Input packet bytes are never modified; outputs are diagnostic projections only.
`packet-generate` always emits a Schema-valid complete P0 packet with unresolved
FDP/Owner, `EVIDENCE_COLLECTION`, and a blocked repair gate; it never converts
capture integrity into implementation authority.

## TUI (P3 — thin shell, same Core)

```
tools/runtime-debug-tui <bundle-dir>     # requires textual:
# UV_CACHE_DIR=.uv-cache uv run --with textual python -m runtime_debug.tui.app <bundle-dir>
```

Rendering only: `execution-tree`/`causal` trees (t/c), errors-only filter (e),
AssetRef panel (a), diagnosis panel (FAILED spans) (d), quit (q). All data comes
from `tui/view_models.py` → `query.py`; the shell never reimplements logic.
`view_models.py` is stdlib-only and unit-tested; the textual import lives only
in `app.py`, deferred so the module compiles without the framework.

## Replay facts (P4a)

```
tools/runtime-debug replay-extract <bundle-dir> --case-id <name>
tools/runtime-debug replay <fixture.json>
```

`replay-extract` mechanically builds a `runtime-debug-replay.v0` fixture
(steps from records, AssetRefs, trace summary, deterministic digest);
`replay` validates one fixture and summarizes it. Minimization is a contract
placeholder only — nothing here mutates or minimizes.

```
tools/runtime-debug replay-run <fixture.json>
```

Deterministic dry-run projection over a fixture: ordered trajectory, action/
observation counts, last observation sequence, and the first mechanically
non-OK step. No device, no state simulation; minimization remains reserved.

```
tools/runtime-debug minimize <fixture.json>
```

Mechanical minimal failure-preserving slice: greedily drops steps while the
dry-run projection still reports the same non-OK result. Read-only; semantic
sufficiency is out of scope.

## Diagnosis workflow (P5)

```
tools/runtime-debug diagnose <good-bundle> <bad-bundle> --case-id <name> [--minimize]
```

One-pass aggregation of structural diff, generated packet, FAILED spans,
replay facts and the §12 evidence gate (projection; never authority). Skill
routing reference: `.ai/skills/evidence-driven-debugging/references/runtime/toolchain-routing.md`.

`packet-generate --out <path>` and `replay-extract --out <path>` write the
artifact JSON to a new file: never inside the bundle, never overwrites,
atomic write. Without `--out` the artifact stays inside the envelope `result`.
