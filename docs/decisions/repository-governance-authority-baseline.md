# PROJECT_LEADER_REPOSITORY_GOVERNANCE_AUTHORITY_BASELINE

> DocumentType: GOVERNANCE_ARCHITECTURE_DECISION
> DecisionId: `repository-governance-authority-baseline`
> Category: `REPOSITORY_GOVERNANCE`
> Status: `FROZEN`
> Decision: `GOVERNANCE_BASELINE_READY`
> DecisionAuthority: `HUMAN_PROJECT_LEADER`
> Date: 2026-08-24
> Scope: Repository governance and documentation authority only.
> CurrentReference: `docs/decisions/repository-governance-authority-baseline.md`
> AuthorityDelta: `CHANGED` — establishes one explicit repository-governance baseline.
> ArchitectureDelta: `ADDITIVE` — does not modify Runtime invariants, protocol semantics, or implementation behavior.

## Human-Compressed Governance Packet

**Goal**

Freeze a repository-governance baseline that makes authority, OpenSpec lifecycle
membership, metadata requirements, Skill authority, and projection synchronization
mechanically decidable.

**What changed / was discovered**

The repository had no single decision that distinguished implementation truth,
lifecycle truth, normative architecture, decision authority, and derived
projections. As a result, `openspec/changes/` contained 16 change bundles while
the current-gates projection and snapshot reported 3 active changes.

**Architecture impact**

Repository-governance authority is clarified and frozen. Runtime Architecture
Contract I-1..I-14, approved OpenSpec semantics, Runtime ownership, protocol
semantics, production code, tests, and change lifecycle state are not modified.

**Material trade-off**

The baseline favors mechanically observable repository state and fail-closed
drift detection over manually curated lifecycle counts. Projections may become
temporarily stale, but they may never override or rewrite their sources.

**Exact decision required**

Adopt Sections 1-6 below as the frozen repository-governance baseline. Authorize
only the bounded documentation cleanup inventory in Section 7; do not execute
that cleanup as part of this decision.

## 1. Source-of-Truth Hierarchy

The repository has five truth layers, ordered from concrete state to derived
interpretation. This is a **typed hierarchy**, not a rule that implementation
can override normative architecture.

1. **Code** — source of truth for what the current implementation actually does
   (as-built behavior and implementation evidence). Code does not authorize
   itself. If code contradicts an approved Contract, spec, or architecture
   boundary, the code is drift or a defect; it does not rewrite the authority.
2. **OpenSpec changes** — source of truth for change existence, lifecycle
   membership, approved change requirements, task progress, and archive state.
3. **Architecture docs** — source of truth for normative architecture,
   responsibility, ownership, invariants, and protocol boundaries, according to
   their declared authority and the repository Authority Order.
4. **Decisions** — source of truth for explicit Human / Project Leader choices,
   gates, receipts, and qualified interpretations. A Decision cannot silently
   override a higher frozen Contract or approved spec; supersession must be
   explicit and authorized.
5. **Projections / snapshots** — derived retrieval views only. They have
   `Authority: NONE` and cannot create lifecycle, architecture, protocol, or
   implementation truth.

When two layers appear to conflict, the question's owner determines the source:
implementation fact → code; OpenSpec lifecycle → OpenSpec; normative boundary →
architecture; explicit gate/choice → Decision. A projection is never selected
over its cited source.

## 2. OpenSpec Lifecycle Authority

- `openspec/changes/` is the source of truth for active change membership.
- `openspec/changes/archive/` is the source of truth for archived change
  membership.
- `docs/work/**`, `docs/snapshots/**`, lifecycle matrices, indexes, dashboards,
  and summaries are projections. They may explain OpenSpec state but cannot
  establish or change it.
- Archive is an explicit lifecycle transition. **Archive is not delete.** A
  Decision, projection, completed task list, graduation receipt, or missing
  buyer does not by itself move or remove a change directory.
- Lifecycle reconciliation must update OpenSpec first. Projections synchronize
  after the source transition; OpenSpec must never be mutated merely to make a
  projection count pass.

## 3. Active Change Definition

An **Active Change** is a direct child change bundle at
`openspec/changes/<change-name>/` that:

1. is not the reserved `archive/` directory; and
2. contains its canonical `proposal.md` change artifact.

Active membership is independent of buyer selection, task completion,
implementation status, validation status, graduation eligibility, deferment,
or long-lived-baseline classification. Those are attributes of an active
change, not membership filters.

A change stops being active only through an authorized archive transition that
places the complete bundle under `openspec/changes/archive/`. Deletion, omission
from a projection, or a historical Decision is not an archive transition.

A direct child without `proposal.md` is a malformed OpenSpec entry. It produces
a structure finding and must not be silently treated as either active or
archived.

At the audited 2026-08-24 working-tree state, all 16 non-archive direct children
contain `proposal.md`; therefore the mechanically derived active count is 16.
This count is audit evidence, not a permanently frozen membership list.

## 4. Decision Metadata Minimum Schema

Every Decision Registry record must contain:

| Field | Rule |
|---|---|
| `ID` | Present and unique. |
| `Title` | Present. |
| `Category` | Required. Historical entries may use explicit `UNDECLARED`; category must not be inferred. |
| `Explicit Status / State` | Present or explicit `UNDECLARED`. |
| `Current Path` | Present and resolves to the record source. |
| `Current Reference` | Resolves to the current source, or is explicit `UNDECLARED`. |

New Decision artifacts must declare at least `DecisionId`, `Category`, `Status`,
`DecisionAuthority`, `Date`, and `Scope`. Historical records are not rewritten
to invent missing facts.

## 5. Skill Authority Metadata

- Every project Skill must declare `metadata.authority`.
- The default and normal value is `NONE`.
- A Skill is an execution method or retrieval aid. It cannot own or mint
  architecture authority, protocol authority, Runtime decision authority,
  lifecycle authority, or Human Gate authority.
- A Skill cannot expand allowed scope. Write or lifecycle authorization must
  come from the user, an approved change, or the applicable authoritative
  Decision/Contract.
- Missing authority metadata is a metadata gap; it is not permission.

## 6. Projection Synchronization Rule

Every projection or snapshot must:

1. declare `Authority: NONE`;
2. cite the authoritative sources from which each material fact is derived;
3. compute OpenSpec active/archive membership from the directory rules in
   Sections 2-3;
4. synchronize after a source change in the same authorized governance change
   set, with source mutation preceding projection update; and
5. fail closed when its statement differs from its source.

On mismatch, the source remains authoritative and the projection is `STALE`.
Agents must report the exact mismatch and bounded recheck scope. They must not
infer lifecycle transitions, buyers, categories, or authority to repair it.

Until affected projections are synchronized, they must not be used to make a
new lifecycle claim. Historical snapshots remain historical and are not
silently rewritten unless their declared role is a mutable latest projection.

## 7. Minimal Follow-up Cleanup Patches — Authorized Inventory Only

The following are separate documentation patches. This Decision identifies
their minimum scope but does **not** execute them:

1. **Current gates mismatch** — update only
   `docs/work/active/current-gates.md` to derive active membership from the
   current `openspec/changes/<change>/proposal.md` inventory; separate active
   membership from buyer/status classification; do not archive or delete.
2. **Snapshot mismatch** — after current-gates passes recheck, update only the
   lifecycle counts/reference in `docs/snapshots/latest.md`.
3. **Decision metadata** — add `Category` to `docs/decisions/index.md`; preserve
   explicit source categories and use `UNDECLARED` for historical entries whose
   sources do not declare one. Do not infer categories from titles.
4. **Skill metadata** — add `metadata.authority: NONE` to the five currently
   missing Skill frontmatters under `.claude/skills/`; do not change Skill
   behavior.
5. **AGENTS path drift** — change the root map statement `code/` to the actual
   implementation root `src/` in `AGENTS.md`; make no other map rewrite.

Each patch requires a bounded diff, `git diff --check`, the relevant metadata or
count recheck, and `scripts/check-consistency.sh`. No Runtime, test, OpenSpec
change artifact, archive membership, architecture baseline, or protocol
semantics modification is authorized by this cleanup inventory.

## 8. Freeze and Conflict Closure

This Decision closes the repository-governance ambiguity that produced
`ARCHITECTURE_DECISION_REQUIRED`. It freezes definitions and authorization
boundaries only; it does not claim the cleanup is complete.

Any future change to Sections 1-6 requires a new explicit governance Decision.
Routine maintenance may synchronize metadata and projections within the frozen
rules but cannot reinterpret them.

Final state: `GOVERNANCE_BASELINE_READY`.
