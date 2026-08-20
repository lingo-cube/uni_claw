# Design: Settings Full-Tree Enumeration Integration

## 0. Reused mechanisms (frozen prerequisites — NOT re-purchased)

`RunOpenWorldAsync` (ONE Agent / ONE Run), run-local ancestry/visited identity
safety, `ContainerInventoryCompletenessEvidence` (frozen discovery epoch,
provenance grounding, positive exhaustion), `PostCompletenessConsistencyValidator`
(non-monotonic evidence), bounded `ScrollBackward` revisit (forward-transition
budget), fresh structured occurrence dispatch, bounded post-action settle
(candidate → confirmation → SETTLED), contextual parent-return control, and
GoalEvidence as the only completion authority. The change composes these; it
does not redefine them.

## 1. Completion definitions (strict)

- **ContainerComplete**: the current Container's inventory has been positively
  exhausted and proven complete (Runtime normalization + provenance acceptance +
  frozen epoch) — the graduated per-Container evidence.
- **SubtreeComplete(C)**: every authorized child branch of C has been visited,
  recursively completed, and verified-returned where applicable — i.e.
  `requiredChildren(C) ⊆ completedChildren(C)`, each with
  `SubtreeComplete(child)` proven and each return verified by fresh evidence.
- **FullTreeComplete**: `SubtreeComplete(Root)` **plus** fresh external
  GoalEvidence / tree-completion evidence evaluated on the fresh accepted
  root observation.

`Root inventory complete` alone is NOT full-tree complete. Dependency direction
is fixed: the recursive subtree proof PLUS fresh external completion evidence
yield FullTreeComplete — `GoalEvidence == true` alone SHALL NOT infer
`SubtreeComplete(Root)` (no reverse derivation).

## 2. Real Settings root entry

Audit (first real slice) of the real Android Settings root:

- application identity: `com.android.settings` (foreground ownership must hold).
- semantic root identity: resolved via the injected semantic-page resolver
  (structured-first, OCR fallback) — the root page name is established by the
  first real observation.
- initial structured sources: the uiautomator structured channel of the real
  root (Search box, sections like Network & internet, Connected devices, Apps,
  Notifications, Battery, …).
- classification per source: `AUTHORIZED_CHILD | UNAUTHORIZED | LOCAL_CONTROL |
  UNRESOLVED` (see §6).
- scrollability: real forward scroll must reveal below-fold sections.
- foreground ownership: settled observations must keep `com.android.settings`.

The root entry slice produces a real root observation record — the basis for
the first recursion scenario. COMPOSE-05 is never used as full-tree evidence.

## 3. Recursion contract

```
Enter C
  prove inventory(C)                (frozen epoch + provenance acceptance)
  for each authorized child source S of C:
      fresh reach S                 (dispatch current visible source)
      settle child C'               (post-action settle)
      recurse C'                    (Enter C')
      prove SubtreeComplete(C')
      verified return C             (fresh evidence reconcile + continuity)
  all required children complete
  SubtreeComplete(C)
```

Constraints: no global graph required; existing ancestry/visited identity
safety remains authoritative (duplicate semantic page identity → fail closed);
depth/budget bounds remain fail-closed; no blind redispatch; no historical
dispatch. The parents stack is the recursion stack (no second authority).

## 4. Child classification

Each discovered source resolves to exactly one of:

- **AUTHORIZED_CHILD** — navigable container child; the ONLY class with a
  recursive completion obligation.
- **UNAUTHORIZED** — discovered but authorization false; recorded, not visited,
  not completed, does not block inventory completeness (per the graduated
  DISCOVERED != AUTHORIZED invariant).
- **LOCAL_CONTROL** — switch/checkable; never a child source.
- **UNRESOLVED** — interactive but identity/meaning not resolvable → blocks
  completeness (fail closed).

`NAVIGATION_CANDIDATE != automatically authorized child`: authorization is a
separate decision via the injected `CandidateAuthorizationEvaluator`.

## 5. Leaf contract

A real Settings leaf is proven only when the truthful completeness conditions
hold: inventory complete (positive exhaustion), zero unresolved interaction,
zero authorized-child obligation. Only then is `LeafSubtreeComplete = TRUE`.
"Currently no navigation candidate visible" alone never proves a leaf (the
existing truthful-leaf invariant).

## 6. Alias / duplicate boundary

Identity safety (duplicate semantic page identity → fail closed) is preserved.
This change does NOT purchase alias merging. If real Settings shows two
different sources resolving to the same semantic destination, classify:
`SETTINGS_DESTINATION_ALIAS_PRESSURE` — STOP; do not relax identity safety.

## 7. External navigation boundary

If a Settings source leads to a system component / external app / dialog outside
Settings ownership, the destination class must be explicitly one of:

- **OWNED_CHILD** — remains within the Settings container tree (same foreground
  ownership); recursive obligation applies.
- **EXTERNAL_BOUNDARY** — foreground ownership leaves Settings; no recursive
  obligation; recorded as boundary (never treated as ordinary child traversal;
  foreground drift is never a child transition).
- **UNRESOLVED** — fail closed.

## 8. Dynamic Settings inventory

If traversal mutates the parent inventory (source added/removed, interactive
Unknown appears, logical-source mapping changes), the frozen-inventory
consistency MUST fail closed (the graduated non-monotonic consistency contract).
This change does NOT implement dynamic graph mutation recovery. Classify:
`SETTINGS_DYNAMIC_INVENTORY_PRESSURE`.

## 9. Completion ledger

Agent-owned run-local completion ledger (per Run). Entries record ONLY proven
facts:

- `ContainerIdentity`
- `ContainerCompletenessEvidence`
- `RequiredChildren` (authorized child source references — recursive
  AUTHORIZED_CHILD obligations only)
- `CompletedChildren` (with their `SubtreeComplete` evidence)
- `SubtreeComplete`
- `VerifiedBoundaryDispositions` — one entry per verified EXTERNAL_BOUNDARY
  source: the source/provenance reference, the verified external-boundary
  evidence, and the disposition. This proves each boundary obligation was
  explicitly handled rather than silently dropped.

Boundary disposition semantics (bookkeeping only):

- `VerifiedBoundaryDispositions` is NOT a graph edge, NOT world truth, NOT a
  recursive child completion, and NOT an authorization authority.
- EXTERNAL_BOUNDARY sources: never recurse, never enter `RequiredChildren`,
  and MUST have a `VerifiedBoundaryDisposition`.
- UNRESOLVED sources still fail closed.

The ledger is completion bookkeeping, NOT a new world-truth authority; external
world evidence remains truth; no global persistent graph (run-local, discarded
after the Run).

## 10. First real scenario — SETTINGS-TREE-01

Real emulator, real Settings root:

```
Root
→ Child A (e.g. a real top-level section)
  → Grandchild A1 (leaf) → verified return
  → sibling A2 (leaf) → verified return
→ verified return Root
→ Root sibling B (leaf or nested) → verified return
→ subtree completion evidence
```

Requirement: ≥3 semantic depths (Root → Child → Grandchild) OR otherwise prove
recursion genuinely occurs — a Root + one-layer-of-leaves replay of COMPOSE-05
is not acceptable.

## 11. Pressure classification

Real runs classify the first production failure only:

- A. `ROOT_EVIDENCE_GAP`
- B. `SOURCE_AUTHORIZATION_GAP`
- C. `RECURSIVE_COMPLETION_GAP`
- D. `SETTINGS_DESTINATION_ALIAS_PRESSURE`
- E. `SETTINGS_DYNAMIC_INVENTORY_PRESSURE`
- F. `EXTERNAL_BOUNDARY_PRESSURE`
- G. `DEPTH_BUDGET_PRESSURE`
- H. `EXISTING_MECHANISM_DEFECT`

Each run fixes at most the FIRST real production failure.

## 12. Architecture / authority

ArchitectureDelta: recorded per approved slices (not claimed NONE if the ledger
or entry slices add structure). AuthorityDelta: NONE — the ledger is bookkeeping;
GoalEvidence remains the only completion authority; the Agent remains the sole
run-level semantic authority.
