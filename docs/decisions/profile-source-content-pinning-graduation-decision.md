# Profile Source Content Pinning — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_PROFILE_SOURCE_CONTENT_PINNING` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-profile-source-content-pinning/`
> Authority: Runtime Architecture Contract I-1..I-14, Architecture v1, and the dsh-uniflow agent-loop plugin design (`docs/decisions/dsh-uniflow-agent-loop-plugin-design.md` — `.ai/` / `.ai/schemas` / `profile-source.yaml` / Python validator as the config and offline-validation truth source, `tools/dsh_profile_adapter.py` as single authority) remain the governing baselines; the change preserves the existing fail-closed `load()` drift/compat gate contract; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** UniFlow/AgentWorkflow profile-source pipeline: `load()` fail-closed drift gate, `binding_revision` / `profile_version` derivation, and commit-gate lock maintenance — per proposal.md (no explicit buyer field is recorded; derived from the proposal's Why section, which states the goal is to stop commit-hash pin false drift — 20 AgentWorkflow use cases hung, production `validate` long-rejected — and align with industry lockfile/content-addressing practice).

This receipt claims only that:

1. `_current_revision()` computes a content fingerprint (sha256 over sorted paths + bytes) of the fixed pin file set — `.ai/profiles/{execution,modules,roles}.json`, `.ai/schemas/{work-item,work-result}.schema.json`, `tools/agent_profile_validator.py` — replacing `git rev-parse HEAD` as the source-revision identity (proposal.md "What Changes"; design.md §1; spec "source revision 身份 = 规则文件集内容指纹");
2. `profile-source.yaml`'s `source_revision` semantics change from commit hash to that fingerprint, the yaml itself does not participate in the fingerprint (self-reference avoidance), and `binding_revision` / `profile_version` continue as `source_revision[:12]`, now fingerprint prefixes (proposal.md "What Changes"; design.md §2 and §3; spec "source revision 身份 = 规则文件集内容指纹");
3. the runtime drift check (`load()` fail-closed) and the compat gate are unchanged — only the measured object changes, with the error message shape preserved (`source revision drift: pinned … != current …`) (proposal.md "What Changes"; design.md §3; spec "fail-closed 兼容门保持");
4. the commit gate `verify-before-commit.sh` detects "pin file set changed but `source_revision` not synced" and prompts with a sync command (non-blocking), and `scripts/sync-profile-pin.py` (idempotent, atomic write, touching no other fields) maintains the lock (proposal.md "What Changes"; design.md §6; spec "提交门维护锁");
5. the pin migration (commit hash → fingerprint, both yaml pins — loose field and JSON block) lands in the same commit as the change (proposal.md "Impact"; design.md §4; spec "迁移一次性完成");
6. workitem `base_revision` keeps commit-hash semantics and is decoupled from the pin (design.md §3; spec "回溯兼容" scenario).

No claim is made for: any change to `.ai/profiles/` or `.ai/schemas/` contents or formats; Runtime/Perception production code; historical `.dsh/profile-adapter/state/` data or existing OpenSpec archive; new abstractions / boundaries / lifecycle changes (the change is classified Medium, within existing contracts); or any change to the fail-closed contract itself (proposal.md "Impact"; design.md "不做").

## 2. Validation evidence

- tasks.md records all 10 tasks complete (`[x]`), including the RED-first cases 1.1 (non-pin change leaves fingerprint stable / pin change alters fingerprint, over a temporary-root pin file set) and 1.2 (`_pin_to_head` and CLI setUp take the fingerprint value while workitem `base_revision` keeps commit hash); implementation 2.1 (fingerprint `_current_revision()`, missing files fail-closed) and 2.2 (drift gate, message shape, `fingerprint()`, schema-version shutter preserved); lock steps 3.1 (`sync-profile-pin.py` atomic/idempotent), 3.2 (migration in the same commit), 3.3 (`verify-before-commit.sh` detection + non-blocking sync prompt).
- tasks.md 4.2 records running the full `tests/AgentWorkflow`, production `validate`, `check-consistency.sh`, and `git diff --check` (completed; no pass/fail counts are recorded).
- tasks.md 4.3 records the manual drift experiment as completed: non-pin change passes / pin content change fail-closed / sync idempotent.
- design.md 「验证」 documents the verification plan: `tests/AgentWorkflow` all-green; production `python3 tools/dsh_profile_adapter.py validate` passing with the new fingerprint pin; manual experiment — non-pin file change → `validate` still passes, one byte changed in a pin file → drift rejection, `sync-profile-pin.py` idempotent; `check-consistency` / `git diff --check`.
- proposal.md "Design Docs" states the implementation is independently re-verified by AgentWorkflow and the consistency gates.

The change's files record no build/test-run numeric evidence — no build result counts, no test counts, no named test files; verification evidence is limited to the completed-task record in tasks.md and the verification plan documented in design.md.

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (no evidence/ directory; design.md contains no falsifier section); rejection/negative requirements are defined in `specs/profile-source-content-pinning/spec.md`:

- **Self-reference avoidance (negative):** `profile-source.yaml` itself MUST NOT participate in the fingerprint (`## Requirement: source revision 身份 = 规则文件集内容指纹`).
- **Precise invalidation (fail-closed):** when any pin-file content changes → fingerprint changes, `load()` MUST fail-closed, existing drift-rejection semantics unchanged (`规则变更精确失效` scenario).
- **Pin file missing (fail-closed):** when any pin file is missing or unreadable → revision resolution MUST error fail-closed, never use a partial file set (`pin 文件缺失` scenario).
- **Compat gate preserved (negative):** schema-version gate and drift rejection MUST keep existing behavior and error message shape (`source revision drift: pinned … != current …`), only pinned/current values change from commit hash to fingerprint (`## Requirement: fail-closed 兼容门保持`).
- **Gate prompts, does not block (negative):** on detected pin drift, verify outputs rule-change identification and the sync command and MUST NOT block the commit; runtime fail-closed is guarded by `load()` (`门提示而非阻断` scenario).
- **Backward compatibility (negative):** pin values must be fingerprint-format; workitem `base_revision` (commit-hash semantics) MUST stay decoupled from the pin, never mixed (`回溯兼容` scenario).

## 4. Deferred scope

The following remain outside this graduation:

- Any change to `.ai/profiles/` or `.ai/schemas/` contents or formats (proposal.md "Impact"; design.md "不做").
- Any change to Runtime/Perception production code (proposal.md "Impact"; design.md "不做").
- Any change to historical `.dsh/profile-adapter/state/` data or existing OpenSpec archive (proposal.md "Impact").
- Any new abstraction, boundary, or lifecycle change — the change is explicitly classified Medium, within existing contracts (proposal.md header).
- Any change to the fail-closed contract itself; drift-rejection and compat-gate behavior are preserved verbatim (design.md "不做"; spec "fail-closed 兼容门保持").

## 5. Final conclusion

**GRADUATED.** The content-fingerprint source-revision identity, preserved fail-closed drift/compat gates, tool-maintained lock, and one-shot pin migration are human-authorized (proposal.md: Human Direction `APPROVED`, 2026-08-29) and evidenced by the recorded completed-task set in tasks.md and the verification plan in design.md; the change's files record no build/test-run numeric evidence. Archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.