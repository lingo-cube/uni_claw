# DSH UniFlow Run-Scoped Operational State — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_DSH_UNIFLOW_RUN_SCOPED_OPERATIONAL_STATE` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-dsh-uniflow-run-scoped-operational-state/`
> Authority: Runtime Architecture Contract I-1..I-14 and Architecture v1, plus the UniFlow adapter contracts this change explicitly preserves (original 12 event names, WorkItem/WorkResult schema, `module-context.json`, `leader-checkpoint.json`, RuntimeAgent authority — per proposal.md) remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** Future UniFlow runs requiring isolated, identity-explicit, evidence-compatible DSH operational state without disturbing the existing dev flow or historical evidence (per proposal.md — proposal.md records no explicit buyer field; derived from its Why section).

This receipt claims only that:

1. a v2 `Session → Run` operational-state layout exists for future UniFlow runs: new Run events and dispatch records are written only into the corresponding Run directory (`sessions/<session_id>/runs/<run_id>/events.jsonl` and `sessions/<session_id>/runs/<run_id>/dispatches/<work_item_id>.json`), with v2 as the single new-write default (no v1 dual-write);
2. non-Run events such as Profile validation/loading remain system-scoped (`system/events.jsonl`, `scope=system`) and are never copied into any Run directory;
3. every persisted Run event carries the explicit identity quartet `session_id` / `run_id` / `correlation_id` (plus `work_item_id` when applicable), sourced from validated dispatch input and its Envelope — never inferred from Host session directory names, which are recorded separately as `host_session_id`;
4. receipt lookup is v2-first with a read-only v1 flat fallback that validates the record's embedded identities and fails closed (`RECEIPT_LOST` / `RECEIPT_MISMATCH`) on mismatch;
5. preserved surfaces are untouched: `module-context.json`, `leader-checkpoint.json`, WorkItem/WorkResult schema, RuntimeAgent authority, the original 12 event names, and explicit `--record-dir` flat compatibility;
6. path formation is fail-closed (single-segment validation rejects absolute paths, separators, `.`, `..`, and traversal; dispatch records keep same-directory temp-file atomic replace), and CLI `validate` uses a non-persistent event sink, so validation has zero side effects on default v1/v2 operational state.

No claim is made for: migration, archival, truncation, or deletion of any v1 history (deferred to a successor change plus independent Human Gate); changes to WorkItem/WorkResult schema, the event-name set, or RuntimeAgent authority; moving `module-context.json` or `leader-checkpoint.json`; C# Runtime/Perception/Replay/Golden Run test assets; or any claim that Host session logs prove UniFlow Run identities they do not contain (all non-goals per proposal.md What Changes and design.md Goals / Non-Goals).

## 2. Validation evidence

- RED-first state-layout and identity tests recorded in tasks.md §1.1–§1.4: persistent EventLog System/Run split, full Run identity, cross-Run isolation, and illegal path-component zero side effects; CLI dispatch default v2 path, same-WorkItem cross-Run no-overwrite, and explicit `--record-dir` compatibility; CLI receipt v2 exact lookup, Session/Run mismatch fail-closed, v1 flat fallback, and Host session id recorded independently; `validate` not modifying default v1/v2 operational state (tasks.md lines 3–6).
- v2 operational-state implementation tasks recorded complete in tasks.md §2.1–§2.5: safe path-component validation, immutable Run event context, and v2 state path resolver; Profile-class events written to system scope and WorkItem/Worker/WorkResult events per explicit context to the corresponding Run, preserving the original 12 event names; default CLI dispatch record to the v2 Session/Run path with `--record-dir` flat compatibility and atomic write retained; receipt v2-first/v1-fallback with Session/Run/WorkItem/owner/binding verification and no Host-session-dir Run-id inference; CLI `validate` on a non-persistent event sink proving zero side effects on default operational state (tasks.md lines 10–14).
- Documentation updated per tasks.md §3.1: `.dsh/profile-adapter/README.md` explains the v2 layout, identity source, v1 fallback, rollback, and the history zero-deletion boundary (tasks.md line 18).
- Human Gate record, 2026-08-29 (tasks.md §3.2, line 20, and the spec's exception clause at specs/dsh-uniflow-run-scoped-operational-state/spec.md lines 58–65, referencing `docs/work/active/dsh-uniflow-v1-events-legacy-migration-gate.md`): a one-time legacy **copy** split of the existing `state/events.jsonl` was authorized (system-class events copied to `system/events.jsonl`, Run-traceable events copied to the corresponding Run files, remainder to `legacy/events.jsonl`); after the split the original file's sha256 is unchanged (`2fa1ca74…`) and dispatch records / `module-context.json` / `leader-checkpoint.json` were not touched.
- Directed AgentWorkflow tests were run and RED→GREEN evidence confirmed per tasks.md §4.1 (line 24).
- Full `tests/AgentWorkflow` suite: **165 passed / 3 subtests passed**; 20 legacy cases previously blocked by profile-source pin drift were migrated to dynamic HEAD via the new `_pin_to_head` helper (same pattern as the CLI suite); the `profile-source.yaml` pin advanced to `3986d3…` per the "protocol change in same commit" rule; production `validate` passes (`VALIDATION_PASS 1@3986d3d…`); `scripts/check-consistency.sh` C1–C15 all PASS; `git diff --check` PASS (tasks.md §4.2, line 26). tasks.md §4.2 also records that OpenSpec strict validation was run as part of this final pass.
- DOCUMENTATION_SYNC check recorded in tasks.md §4.3 (line 27): architecture / Runtime / current projection NO_CHANGE; successor change documentation updated; decision and main-spec sync deliberately deferred to archive — the main-spec sync is executed by this batch.
- The change's files record no `dotnet build`/C# test-run evidence — the change explicitly excludes C# test assets (proposal.md Impact); verification evidence is the adapter + AgentWorkflow evidence recorded above. The change directory contains no `evidence/` subdirectory; all evidence is recorded in tasks.md.

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section; rejection/negative requirements are defined in specs/dsh-uniflow-run-scoped-operational-state/spec.md:

- **新 Run 使用版本化 Session/Run 状态布局**: adapter MUST NOT infer UniFlow `run_id` from Host session directory names; a Host session id MAY be recorded as an independent evidence field, but the directory name MUST NOT override the dispatch record's UniFlow `session_id` or `run_id`.
- **Run 事件携带完整关联身份**: Profile source validation/loading/conflict events with no Run input MUST be recorded `scope=system` and MUST NOT be copied into any Run directory; no Run directory MUST be created when the adapter only validates or loads a Profile.
- **v2 receipt 精确查找并保留 v1 只读兼容**: on embedded-identity mismatch the adapter MUST return `RECEIPT_LOST` or `RECEIPT_MISMATCH` and must not guess; a receipt lookup pointing at another Run MUST fail-closed and MUST NOT accept the WorkResult or ModuleContext Delta; the v1 fallback MUST NOT rewrite or migrate the flat record.
- **历史状态在本 change 中保持冻结**: this change MUST NOT migrate, rewrite, truncate, or delete the existing `.dsh/profile-adapter/state/events.jsonl`, flat dispatch records, ModuleContext, LeaderCheckpoint, or OpenSpec/archive evidence; when v2 dispatch/receipt/validation commands run, v1 historical file content and mtime MUST remain unchanged (sole exception: the 2026-08-29 Human Gate authorized a one-time legacy **copy** split that only adds copied files).
- **状态路径与写入 fail-closed**: absolute paths, separators, `.`, `..`, and traversal inputs MUST be rejected before any state write; rejected identities MUST NOT produce partial directories, events, or records.
- **验证命令不污染持久运行状态**: validation-only commands MUST use a non-persistent event sink or isolated temporary state and MUST NOT append events to the repo default operational state; default v1 and v2 event file content and mtime MUST remain unchanged.

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- Migration, archival/retention, and deletion of v1 history — successor change plus independent Human Gate (proposal.md What Changes; design.md Non-Goals and Migration Plan step 5).
- Cross-process event-log locking and history compaction (design.md Risks / Trade-offs).
- Freezing a Session-scoped checkpoint model and moving `leader.fallback.started` / `checkpoint.updated` under Session scope (design.md Decision 4).

## 5. Final conclusion

**GRADUATED.** The v2 Session/Run-scoped operational-state layout, explicit Event identity handling, system/run event split, v2-first/v1-fallback receipt verification, fail-closed path and identity guards, and the frozen v1 history boundary are human-authorized and bounded by the evidence recorded in the change's own files (all 14 tasks in tasks.md complete, full suite 165 passed / 3 subtests passed, `VALIDATION_PASS 1@3986d3d…`, check-consistency C1–C15 PASS). Archival of the change under `openspec/changes/archive/2026-08-30-dsh-uniflow-run-scoped-operational-state/` is performed on 2026-08-30 as a separate lifecycle operation in this batch.
