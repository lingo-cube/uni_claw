# Test Environment Dependency Audit

> Companion evidence to
> `docs/decisions/test-environment-dependency-audit-report.md` (the
> PROJECT_LEADER_TEST_ENVIRONMENT_DEPENDENCY_AUDIT_REPORT).
> Date: 2026-08-23 · Worker: DeepSeek-V4-Flash · Reviewer: DeepSeek-V4-Pro

## RealDevice requirement matrix

### Architecture-proof tests (NO device)

These prove Runtime generic capability through the deterministic fake world and
require only the Runtime assemblies:

| Suite | Location | Device | Assets |
|---|---|---|---|
| Evidence Specification tests (Generic Tree/Diamond world, Settings-as-fixture) | `tests/UniClaw.Runtime.Tests/Evidence/` | none | none |
| OpenWorld / Semantic / Strategy / Pre-terminal deterministic suites | `tests/UniClaw.Runtime.Tests/Scenario/` (deterministic halves) | none | none |
| Replay / Perception asset suites | `tests/UniClaw.Runtime.Tests/{Replay,Perception}/` | none | repo assets (`Assets/**`) |
| Vision host behavioral proofs | `tests/UniClaw.Runtime.Tests/Vision/` | none | python3 (stdlib) |
| DriverHost wire tests (loopback only) | `tests/UniClaw.Runtime.Tests/DriverHost/` | none | none |

### Hardware-integration tests (device required)

| Test | Why real device | Why fake is insufficient | Required device/software state | Expected output |
|---|---|---|---|---|
| `SettingsSingleRecursiveChild_RealDevice_Phase2` | Prove ContainerComplete(Root)→one authorized child→ContainerComplete(Child) on the REAL Android Settings app through the production pipeline (screenshot→vision→structured→Agent) | The graduated claim is about the real external world; fake world can only prove the mechanism, not the device integration | Online emulator (AOSP, `com.android.settings` system app), local vision service socket | `RunState.Completed`, evidence dump at `/tmp/...`; ContainerComplete both levels |
| `SettingsGrandchildVerifiedReturn_RealDevice_Phase3` | Parent-return across two levels on real Settings (Location→Location services→return) | Same | Same as above | Completed; verified parent-return trace receipts |
| `SettingsSiblingSubtreeLedger_RealDevice_Phase4` | Sibling subtree completion ledger on real Settings root | Same | Same as above | Completed; two siblings completed with order |
| `SettingsTreeCapstone_RealDevice_Phase5` | TREE-1..TREE-20 end-to-end on real Settings | Same | Same as above | Completed; full tree ledger |
| `SettingsRoot_RealDevice_Phase1_RootContainerRealityBaseline` | Root reality baseline: ContainerComplete(Root) on real Settings root, no recursion | Same | Same as above | Completed; root completeness only |
| `ExternalBoundary_RealDevice` | External boundary: app-permission dialog foregrounds `com.android.permissioncontroller`; Runtime must not treat external foreground as owned | Fake world cannot produce a real cross-app foreground | Online emulator with `com.android.permissioncontroller`; permission state resettable | Completed with boundary handling (or documented EBD outcome) |
| `Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete` | SINGLE_AGENT_FULL_RUN_CAPSTONE (COMPOSE-05) on real emulator through the full pipeline | Same | Online emulator + `com.uniclaw.fixture` APK (built + installed) + vision service | Completed; goal evidence satisfied (`Visited 8/8 [CAPSTONE COMPLETE]`) |

### Failure classification (per test)

| Failure | Class |
|---|---|
| Assertion/trace mismatch while device+assets present | `CodeFailure` |
| `adb devices` empty / serial not found / emulator not booted | `EnvironmentUnavailable` |
| vision socket connect fails (`INFRASTRUCTURE_FAILURE` perception outcome) | `EnvironmentUnavailable` |
| `com.uniclaw.fixture` not installed / APK missing at build path | `MissingDependency` |
| `node` not on PATH for DriverHost E2E | `EnvironmentUnavailable` |
| `setupRunner` force-stop/start/dump command fails while device is online | `SetupFailure` |

## APK / external asset inventory

| Asset | Location | Producer | Consumed by | Classification |
|---|---|---|---|---|
| `fixture-debug.apk` | `tools/android-runtime-reality-fixture/build/` | `scripts/build.sh` (documented, in-repo) | Capstone real-emulator suite | **A — documented dependency** |
| Settings semantic package | `src/UniClaw.Semantic.Settings` (in-repo project) | repo | Runtime.Tests project reference | **A — documented (csproj)** |
| Semantic corpus (in-memory) | `tests/Semantic/CorpusTests/` | code | Semantic.Tests | A — no external file |
| Perception assets | `tests/UniClaw.Runtime.Tests/Perception/Assets/**` (PNG/JSON) | repo | Perception/Replay suites | A — copied to output via csproj `Content` |
| Replay assets | `tests/UniClaw.Runtime.Tests/Replay/Assets/**` (XML/JSON) | repo | Replay suites | A — copied to output via csproj `Content` |
| `vh_test_server.py` | `tests/UniClaw.Runtime.Tests/Vision/` | repo | VisionHostBehavioralProofs | A — copied to output via csproj `Content` |
| Vision YOLO model `best.pt` | `platforms/perception/models/...` | ML training (not in test scope) | **NOT consumed by tests** — vision-host tests use fake temp files | A (out of test scope) |
| `dsh-plugin-uniclaw/test/*.mjs` | `dsh-plugin-uniclaw/test/` | repo (ESM, no build) | DriverHost node E2E | A — documented (node on PATH) |

## Hidden-dependency findings

- **No hidden APK installation:** the only APK (fixture) is installed by the
  documented `tools/android-runtime-reality-fixture/scripts/install.sh`, whose
  usage is documented in `tools/android-runtime-reality-fixture/README.md`.
- **No undocumented adb command:** every adb invocation lives inside the
  real-device test methods (visible in source) or in the documented install
  script. No external script mutates device state behind the tests' back.
- **No manually prepared emulator state:** emulator boot is a documented
  prerequisite (manifest); tests perform their own in-test setup
  (`am force-stop` / `am start` / readiness polling) and clean up nothing
  persistent (device state is disposable).
- **Serial/adb-path resolution is explicit:** `RealDeviceTestConfiguration`
  resolves `UNICLAW_ADB_PATH` / `UNICLAW_SETTINGS_SERIAL` /
  `UNICLAW_CAPSTONE_SERIAL` env overrides → discovery of the unique online
  device → clear failure. No machine-specific default is baked into tests.
- **No network dependency:** all `TcpClient` uses are loopback to an in-test
  DriverHost server (`127.0.0.1`). No external service is contacted.
- **No model/corpus hidden loading in tests:** vision-host tests synthesize
  fake `best.pt`/`server.py` temp files; perception tests load committed repo
  assets only.

## Preparation / cleanup checklist (new engineer)

```text
# Deterministic suites (default):
dotnet build src/UniClaw.Runtime.sln -p:NuGetAudit=false
dotnet test tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj -p:NuGetAudit=false
dotnet test tests/Semantic/Semantic.Tests.csproj -p:NuGetAudit=false

# Real-device suites (optional, hardware required):
1. Boot emulator (AVD, AOSP API 35):  $ANDROID_SDK_ROOT/emulator/emulator -avd <avd> &
2. adb wait-for-device; check sys.boot_completed=1
3. (Capstone only) bash tools/android-runtime-reality-fixture/scripts/build.sh
   && adb -s <serial> install -r tools/android-runtime-reality-fixture/build/fixture-debug.apk
4. Start vision service:
   cd platforms/perception && ../../.venv-local-vision/bin/python -m uvicorn \
     uniclaw_perception.server:app --uds /tmp/uniclaw-capstone.sock &
5. dotnet test ... --filter "FullyQualifiedName~_RealDevice_|FullyQualifiedName~RealEmulator"
6. Cleanup: stop uvicorn; rm /tmp/uniclaw-capstone.sock; adb emu kill
```
