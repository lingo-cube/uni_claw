# Semantic Object + Capability Model — Module Delivery Record

> Phase 2 of 6 | Status: ACCEPTED | 2026-08-11
> Baseline: `docs/decisions/semantic-agent-runtime-target-architecture-review.md`

## Module Contract

| Item | Detail |
|---|---|
| **SemanticObject** | `sealed record` — immutable declarative domain concept. Identity + Category + StateDimensions. No UI details. No mutable state. No owner. |
| **Capability** | `sealed record` — immutable declarative domain contract. Name + ApplicableToCategory + StateDimension. No execution procedure. No owner. |
| **Wi‑Fi Slice** | WifiConnectivity (ConnectivitySetting, [Enabled]) + BluetoothConnectivity (ConnectivitySetting, [Enabled]) + SetEnabled capability |
| **Second Domain** | BluetoothConnectivity uses same contract — proves model is not Wi‑Fi-specific |

## Production Delta

| File | Type | Role |
|---|---|---|
| `Model/SemanticObject.cs` | `sealed record` | Immutable domain object definition |
| `Model/Capability.cs` | `sealed record` | Immutable capability contract |

2 new files. 0 existing files modified. 0 mutable state added. 0 new owners.

## Verification

- 18 targeted tests (P1-P9 + Wi‑Fi vertical slice proofs)
- 592/592 full regression (0 failures)
- Architecture guards pass

## Acceptance

- P1: SemanticObject contains NO UI element fields ✓
- P2: Capability contains NO execution procedure ✓
- P3: StateDimensions ≠ current state values ✓
- P4: Immutability (sealed record, value equality, with-expression) ✓
- P5: No mutable owner (both are records, no mutable fields) ✓
- P6: Multi-domain (Wi‑Fi + Bluetooth share contract) ✓
- P7: Architecture direction unchanged ✓
- P8: Existing Runtime unchanged semantically ✓
- P9: Validation (null/empty rejection) ✓

## Architecture Check

| Check | Status |
|---|---|
| UiDetailsLeakedIntoDomainModel | NO |
| MutableOwnerAdded | NO |
| AgentAuthorityChanged | NO |
| TraversalAuthorityChanged | NO |
| ArchitectureDelta | NONE |
