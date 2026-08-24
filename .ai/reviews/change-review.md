# Change Review Checklist

> Mandatory pre-submission review for any code change, by risk level
> (`.ai/development-protocol.md` §17). Supersedes
> `.ai/reviews/runtime-change-review.md` (its Runtime section is now part of
> this document). Companions: `.ai/skills/evidence-driven-debugging/` and
> `.ai/skills/runtime-behavior-debugging/`. Read-only review; record findings
> per section, then issue a verdict.

## Change Type

- [ ] Runtime change (Agent loop / FSM / Traversal / Recovery / Semantic
      boundary / coverage-revisit seams) → evidence level **E3** required;
      real-device touch → **E4**
- [ ] Test change → capability-model check below
- [ ] Architecture change (ownership / authority / dependency direction /
      invariant) → STOP: this requires the Architecture / Human gate per
      `.ai/development-protocol.md` §7 — a review checklist cannot authorize it

## 1. Authority (责任边界)

- [ ] Does the change add execution authority anywhere?
- [ ] Does it change a responsibility boundary (owner / decision authority)?
- [ ] Does it bypass the Agent / FSM / GoalEvidence decision surface?
- [ ] If Runtime-core: `AuthorityDelta: NONE | CHANGED` and
      `ArchitectureDelta: NONE | ADDITIVE | BREAKING` stated explicitly.

## 2. Evidence (事实基础)

- [ ] Is the change grounded in collected evidence at the required level
      (L2→E1-E2, L3→E3, L4→E4)?
- [ ] Was the failure CLASSIFIED (Discovery / Grounding / Authorization /
      Execution / Recovery / Environment) before the change, with proof —
      not inferred from the symptom?
- [ ] Are hidden assumptions (device behavior, OCR stability, swipe physics,
      timing) stated explicitly with the evidence that supports them?

## 3. Boundary (依赖边界)

- [ ] Does the change introduce scenario knowledge into production
      (Settings logic, child-index / list-size assumptions, coordinate
      memory, OCR special rules)?
- [ ] Does it promote fixture / test knowledge into production logic?
- [ ] Are the semantics scenario-neutral (generic tree / viewport / source
      classes only)?
- [ ] Does the change create a wrong dependency direction
      (Agent → Container → Traversal → Environment)?

## 4. Testing (能力验证)

- [ ] Do the tests verify CAPABILITY (coverage, authorization, consistency,
      fail-closed, evidence sufficiency) — not scripts?
- [ ] Any fixed click counts / fixed ActionHistory / fixed page paths /
      fixed coordinates / fixed UI copy?
- [ ] Do deterministic tests use EvidenceFixture + ExpectedSpecification +
      Runtime Execution + Evidence Evaluation?
- [ ] Is the regression scope appropriate (related suites + full suite +
      real-device re-run when touched)?

## Verdict

- [ ] APPROVE
- [ ] APPROVE-WITH-NOTES (list notes)
- [ ] REJECT (list blocking findings — any Authority violation, scenario
      knowledge / wrong dependency, or scripted test is blocking)

Reviewer: ________  Date: ________
