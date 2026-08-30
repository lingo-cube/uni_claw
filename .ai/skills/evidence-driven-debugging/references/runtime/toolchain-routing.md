# Runtime Debug Toolchain Routing (P5)

> Status: `ROUTING_REFERENCE / NO_AUTHORITY`
> Boundary: READ_ONLY · DETERMINISTIC · NO_RUNTIME_AUTHORITY

When an E2/E3/E4 Runtime/FSM/Traversal/Perception/Fusion/Semantic/Completeness
failure occurs, the debugging workflow SHALL offer the toolchain command
sequence below before any implementation WorkItem:

```
runtime-debug assets <bad-bundle>                          # AssetRef first-class
runtime-debug packet-generate <bad-bundle> --case-id <case># base Debug IR packet
runtime-debug run-compare <good-bundle> <bad-bundle>       # structural axes diff
runtime-debug execution-tree <bad-bundle> --only-errors     # FAILED span spine
runtime-debug terminal-chain <bad-packet>                   # terminal + stored diag
runtime-debug replay-extract <bad-bundle> --case-id <case>  # replay fixture
runtime-debug replay-run <fixture>                          # dry-run trajectory
runtime-debug minimize <fixture>                            # mechanical falsifier slice
runtime-debug diagnose <good-bundle> <bad-bundle> --case-id <case>
                                                           # one-pass report + gate
```

Workflow: Freeze Reality → Query Run → Find First Blocker → Build Evidence
Packet → Find Good/Bad Pair → Trace Diff → LAST_GOOD/FIRST_BAD → FDP → Owner →
GapKind → Disposition.

Implementation gate (projected, never authored): an implementation WorkItem
SHALL be permitted only when `diagnose` reports `fdpPresent=true`,
`evidenceRefsPresent=true`, and the Agent confirms Owner/GapKind; otherwise the
only permitted action is `EVIDENCE_COLLECTION`. `NO_FDP → NO_IMPLEMENTATION`,
`NO_OWNER → NO_IMPLEMENTATION`.

All tooling output is a diagnostic projection; it never changes Runtime,
Trace, wire, identity, or decision authority. Assets referenced via AssetRef
never become world truth.
