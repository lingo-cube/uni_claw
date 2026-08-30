# Runtime Debug P1a Conformance Revalidation

> DocumentType: `NON_NORMATIVE_CONFORMANCE_RESULT`
> Status: `PASS_AFTER_MINIMAL_REPAIR / P1_GRADUATION_NOT_REOPENED`
> Date: 2026-08-30
> Authority: `NONE`
> Scope: offline `runtime-debug` packet reader, occurrence projection, canonical envelope, and test hygiene
> AuthorityDelta: `NONE`
> ArchitectureDelta: `NONE`
> RuntimeBehaviorDelta: `NONE`

## 1. Trigger and lifecycle boundary

Repository inventory found that Runtime Debug P1a–P1d were already graduated and archived before a new P1 implementation slice was needed. This revalidation therefore did not create a duplicate active change, rewrite the archived graduation decisions, or authorize P2 graduation.

The current lifecycle sources remain:

- `docs/decisions/runtime-debug-p1a-summarize-occurrence-graduation-decision.md`;
- `openspec/changes/archive/2026-08-30-runtime-debug-p1a-summarize-occurrence/`;
- P1b–P1d corresponding graduation decisions and archive bundles.

## 2. Revalidation finding

The existing happy-path suite was green, but direct falsifiers exposed four P1a conformance gaps:

1. the packet reader accepted unknown versions by prefix instead of requiring exact `runtime-debug-evidence-packet.v0`;
2. the occurrence mismatch, ambiguity, and insufficient-coverage branches called the fail-closed helper with the wrong arity, so the CLI fallback returned `SCHEMA_VIOLATION` instead of the required closed status;
3. a missing absolute packet path was echoed in diagnostics and the failure envelope lost the requested command name;
4. the capture-bundle reader and test fixture helpers left file handles unclosed, producing `ResourceWarning` noise.

The ambiguity scenario also clarified an existing spec/design tension. The authoritative P1a spec requires `AMBIGUOUS_OCCURRENCE` to return all candidates in deterministic order without selecting a winner. The implementation now follows that scenario while retaining a nonzero exit code and fail-closed status.

## 3. Minimal repair

- require exact packet and Debug IR v0 version values;
- validate EvidenceIndex entries as objects and resolve TargetOccurrence EvidenceRefs;
- return the declared mismatch, ambiguity, and insufficient-coverage statuses;
- retain all ambiguity candidates in deterministic order and never emit a winner;
- keep the requested command and suppress absolute input paths in reader failures;
- make the last-resort error message environment-independent;
- close all reader and fixture file handles;
- synchronize the local README with the command surface already present in the checkout.

No Runtime, Trace, Span, Event, Harness model, DriverHost, wire/API, dependency, device, network, repair authority, Owner authority, or lifecycle behavior changed.

## 4. Verification evidence

| Gate | Result |
|---|---|
| P1/P2 runtime-debug focused suite with `ResourceWarning` as error | `44 tests / PASS` |
| Full stdlib AgentWorkflow discovery | `212 tests / PASS` |
| `runtime-debug-p2a-run-compare` strict OpenSpec validation | `PASS` |
| `runtime-debug-p2b-trace-diff` strict OpenSpec validation | `PASS` |
| `runtime-debug-p2c-terminal-chain` strict OpenSpec validation | `PASS` |
| `runtime-debugging-toolchain` strict OpenSpec validation | `PASS` |
| repository consistency C1–C15 | `ALL PASS` |
| scoped `git diff --check` | `PASS` |

The new falsifiers cover exact packet version, `IDENTITY_MISMATCH`, `AMBIGUOUS_OCCURRENCE` with two ordered candidates and no winner, `INSUFFICIENT_TRACE_COVERAGE`, absolute-path suppression, and command preservation.

## 5. Current conclusion

P1a–P1d are present, graduated, and independently revalidated after the minimal conformance repair. The read-only toolchain still has no Runtime or Trace authority.

P2a–P2c currently have completed implementation tasks and strict-valid active OpenSpec bundles, but this document does not graduate or archive them. Their next step is a separate Human lifecycle gate using their own spec, focused tests, and buyer claim boundary.
