# AGT-003 — Implementation plan

1. Add realization-private DSH session retention in `DshOpenedHttpPeer`; include it in consult requests and clear it on detach/dispose.
2. Validate explicit session, Product Session, and Product Run mappings in the DSH plugin without changing Product semantic records.
3. Add wire and plugin regression tests for two consultations, reuse, mismatch rejection, and current `DecisionId` diagnostics.
4. Run focused tests, full build, scenario/certification checks, then retry the real DSH Settings traversal with elevated device access when required.
5. Review the diff for AGT-001/AGT-002 boundary violations. Keep the change at HOLD until the existing Product Host closure and terminal-outcome evidence gates are resolved; do not widen authority in this change.
