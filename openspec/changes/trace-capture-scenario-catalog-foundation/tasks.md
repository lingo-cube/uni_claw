# Tasks: trace-capture-scenario-catalog-foundation

> Planning artifacts transcribe the approved architecture gate. Human Apply authorization for TC-01 through the validation portion of TC-07 was granted on 2026-08-22. Graduation and archive remain separately gated.

## 1. TC-00 — OpenSpec purchase

- [x] 1.1 Create proposal, design, four delta specs, and implementation task graph from the approved architecture gate
- [x] 1.2 Obtain explicit apply authorization before changing production or test code

## 2. TC-01 — Reusable Harness contracts

- [x] 2.1 Add `UniClaw.Runtime.Harness` and move reusable asset, validation, JSON, and replay contracts without duplication
- [x] 2.2 Preserve existing manifest/replay behavior and add guards proving Runtime does not reference Harness or Adapters

## 3. TC-02 — In-memory capture lifecycle

- [x] 3.1 Implement immutable capture bundle/result, `TraceCaptureSession`, and `CapturingEnvironment`
- [x] 3.2 Prove ordering, correlation honesty, Runtime trace snapshot honesty, failure isolation, and distinct Runtime/capture outcomes with an in-memory store

## 4. TC-03 — Append-only persistence

- [x] 4.1 Implement the narrow capture-store contract and atomic append-only filesystem store
- [x] 4.2 Prove staging, hashing, collision refusal, cancellation cleanup, and absence of partial publication

## 5. TC-04 — Physical artifact attachment

- [x] 5.1 Add the narrow optional physical artifact tap for screenshot, perception, and final Observation correlation
- [x] 5.2 Prove artifact capture faults cannot escape into physical observation or dispatch behavior

## 6. TC-05 — Immutable Scenario catalog

- [x] 6.1 Implement explicit catalog loading, lookup, and schema/reference/hash/provenance validation
- [x] 6.2 Prove duplicate, dangling, path-escape, version, hash, and provenance failures stop before replay

## 7. TC-06 — Canonical golden replay

- [x] 7.1 Represent already-ON and OFF-to-ON golden cases as reviewed Scenario and Replay assets
- [x] 7.2 Prove catalog replay equivalence before removing hard-coded assembly, preserving source evidence without inferred Runtime facts

## 8. TC-07 — Validation and graduation

- [x] 8.1 Run targeted capture/catalog/replay scenarios SC-TC-001..004, SC-CAT-001..002, and SC-REG-001
- [ ] 8.2 Run full regression, architecture guards, consistency, strict OpenSpec, provenance, and sensitive/raw-asset audit
- [ ] 8.3 Record an independent implementation result and obtain the required Human graduation receipt before archive
