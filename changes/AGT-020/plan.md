# AGT-020 — Plan

> Change State: `changes/AGT-020/state.md`
> Baseline: `3568a04e`
> Owner: Leader；机械同步和定向测试可交给 Worker，但不改变冻结字段。

## Slice A — Artifact alignment

- Read `ProductProtocolSchema.Current` and current `schema-hash.txt`.
- Update canonical `product/prompt/uniagent-prod/manifest.json` to that schema
  hash and bump `promptRevision` once.
- Recompute canonical `prompt-hash.txt`, update `product/profiles/uniagent-prod.yaml`
  assembly hash/revision, and synchronize the tracked DSH package copy.
- Run protocol schema, prompt-manifest and package-sync checks.

## Slice B — Dedicated bridge preflight

- Deploy the four tracked DSH packages into the isolated
  `~/.dsh/profiles/uniclaw-product` snapshot using `dsh/deploy.sh` with an
  explicit profile directory; never mutate the owner profile or port 3080.
- Start `dsh/test-service.sh` with `DSH_TEST_PROFILE=uniclaw-product` on 3081.
- Run HTTP health, prompt-probe, schema-hash and handshake preflight; save
  sanitized evidence under `evidence/agt-020/`.

## Slice C — Real bridge capture

- Run `DshOpenedHttpPeerE2eTests` and capture the outbound request boundary.
- Confirm all frozen fields and the same event cutoff survive to the DSH
  response/diagnostic record. If LocalView is absent, stop and localize before
  editing code.
- Re-run deterministic DSH tests and plugin tests after any seam fix.

## Slice D — Registered device run

- Start the registered `p26_pixel` through `tools/android/start-test-device.sh`.
- Run one bounded settings flow with `UNICLAW_DSH_E2E_BASE=http://127.0.0.1:3081/`
  and the existing live verification entry point.
- Persist preflight, trace, consultation, effect receipts, bridge audit and
  cleanup evidence; stop honestly on any environment gate.

## Slice E — Review / Verify / Close

- Run `dotnet test UniClaw.Kernel.slnx --no-restore`.
- Run `python3 tools/scenario_certify.py --change AGT-020 --all`, then
  `--check`; run `git diff --check` and generated artifact checks.
- Review only AGT-020 files, update state to `closed` only if every acceptance
  row has evidence, regenerate `changes/INDEX.md`, commit and push.
