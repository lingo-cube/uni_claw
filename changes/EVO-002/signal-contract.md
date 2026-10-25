# Evolution signal v0

Scope: Harness evidence only. A signal never creates a Change or grants repair authority.

- schema: uniclaw.evolution.signal.v0; authority: non-authoritative.
- lane: dev-verify | live-run.
- source: {kind: trx | runDir | composition | verification-exit, path?, sha256?}.
- verdict: failed | hash-mismatch | unknown. preliminaryClass: A-F or null when evidence cannot classify it. Never infer causal Owner from dirty paths.
- failureKey: SHA256 of lane + stable failure identity, excluding emission time/TRX execution id.
- context: dev includes intent, git head/dirtyPaths/gitAvailable and verifier change/scope when available; live includes observed runStatus/terminal/reason.
- ts: UTC emission time. diagnosis?: {path, sha256}, reference only.
- reproduce: dev supplies exact test filter only (project/immutable inputs still require LOCK); live is null with replayStatus=pending-reproduction.
- triage: candidate | duplicate | expected-red | known-issue; changeCreated always false. Intake files are dedup evidence, not case manifests or another lifecycle.

Entry points: python3 tools/evolution-signal.py dev --trx PATH --intent verify|expected-red --out-dir OUT [--composition PATH] [--known-issues JSON]; python3 tools/evolution-signal.py run --run-dir DIR --out-dir OUT [--diagnosis PATH]. OUT must be outside the input run for live inspection. Unknown/malformed input emits unknown, not a success claim. A completed run requires status=Completed AND terminal=true; recognized active statuses emit no failure candidate.

Known issues file: {"issues":[{"failureKey":"<hex>","ref":"changes/ID/state.md"}]}. Open case manifests with signal.failureKey are detected directly; legacy locked manifests without fingerprints are not rewritten and can be mapped by an explicit known-issues reference. No model classification is used for deterministic triage.

Verifier hook: verify-change uses unique current-execution TRX names for every test project. No old TRX is substituted when a build fails. Existing verdict/exit code is preserved; missing test evidence yields unknown. The scanner reads composition pins itself instead of deriving manifest ownership from console prose.

Production hook: the existing run-diagnosis model-procedure declares entry=tools/evolution-signal.py as its deterministic exit adapter while skillRef remains the sole model procedure. The entry receives --run-dir/--out-dir/--diagnosis; it inspects facts and references the newly exported packet. This uses existing registry fields and creates no new Tool or runtime Owner. Tools lacking an exit entry still export their packet. The script also accepts these direct entry arguments (equivalent to the run subcommand). Output posture is local-write because evidence artifacts are created; run inputs and Runtime state remain read-only.

Diagnosis output: schemas/diagnosis-packet.schema.json is the mechanical form of the debug-evidence reference. The Host stamps actual producer/model/report hash, validates diagnostics and rejects malformed or authority-claiming responses. It writes evidence/diagnosis/<unique>/diagnosis.packet.json, returns packetRef/packetSha256, and exposes exit signalRefs. No real provider/device validation is implied by deterministic transport tests.

Review corrections: composition scan runs even when requested tests passed; its signal does not rewrite the original scope verdict. Unreadable/malformed composition yields unknown alongside already parsed TRX failures. Expected-red only suppresses observed Failed TRX results, never malformed evidence or drift. Signal schema mechanically requires both diagnosis path and sha256.
