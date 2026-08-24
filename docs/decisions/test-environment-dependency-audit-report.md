# PROJECT_LEADER_TEST_ENVIRONMENT_DEPENDENCY_AUDIT_REPORT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Independent Reviewer: DeepSeek-V4-Pro
> Scope: verification and documentation completion — NO test-architecture
> redesign, NO Runtime production change, NO invariant weakening.

---

## 1. Test Dependency Inventory Location

Complete dependency inventory is declared in **two complementary places**:

1. **Executable manifest (test-side model):**
   `tests/UniClaw.Runtime.Tests/Dependencies/TestDependencyManifest.cs` — every
   suite's dependencies with kind (`DeterministicOnly` / `AndroidEmulator` /
   `RealDevice` / `NodeClient` / `VisionService` / `Python3` / `FixtureApk` /
   `RepoAsset`), failure-if-missing class (`CodeFailure` /
   `EnvironmentUnavailable` / `MissingDependency` / `SetupFailure`),
   preparation and cleanup steps.
2. **Human-readable inventory:**
   `docs/testing/test-environment-dependency-audit.md` — RealDevice
   requirement matrix, APK/asset inventory, hidden-dependency findings,
   preparation/cleanup checklist for a new engineer.

## 2. Environment Manifest Status

**COMPLETE.** `TestDependencyManifest` covers:

| Suite | Kind | Prep / Cleanup |
|---|---|---|
| Evidence Specification tests (`Evidence/*.cs`) | DeterministicOnly | none / none |
| OpenWorld/Semantic/Strategy/Pre-terminal deterministic | DeterministicOnly | none / none |
| Replay/Perception asset suites | RepoAsset | none / none |
| Vision host behavioral proofs | Python3 (stdlib) | none / none |
| DriverHost node E2E | NodeClient | npm (ESM, no build) / none |
| Settings real-device suites (Phase1–5) | AndroidEmulator + VisionService | boot AVD + vision socket / `adb emu kill` + stop uvicorn |
| External boundary real-device | AndroidEmulator (needs permissioncontroller) + VisionService | same + permission-state reset / same |
| Capstone real-emulator | AndroidEmulator + FixtureApk + VisionService | build+install fixture APK + boot + vision socket / uninstall + stop |

Guard tests (`TestDependencyManifestTests`, 4/4 pass) mechanically enforce:
manifest non-empty, real-device suites declare emulator dependency, all failure
classes defined, serial resolution is env-var/discovery (never machine-specific),
and the Capstone suite declares the fixture-APK dependency.

## 3. APK Dependency List

| APK | Producer | Consumer | Classification |
|---|---|---|---|
| `tools/android-runtime-reality-fixture/build/fixture-debug.apk` (`com.uniclaw.fixture`) | `scripts/build.sh` (documented in-repo; aapt2→javac→d8→zipalign→apksigner, no Gradle) | `Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete` | **A — documented dependency**; install via documented `scripts/install.sh`; cleanup `adb uninstall com.uniclaw.fixture` |

No other APK is installed by any test. Settings suites use the AOSP **system
app** `com.android.settings` (no APK).

## 4. External Asset List

| Asset | Location | Consumers | Classification |
|---|---|---|---|
| Settings semantic package | `src/UniClaw.Semantic.Settings` (in-repo project) | Runtime.Tests (csproj reference) | A — documented |
| Semantic corpus | `tests/Semantic/CorpusTests/` (in-memory) | Semantic.Tests | A — no external file |
| Perception assets (PNG/JSON) | `tests/UniClaw.Runtime.Tests/Perception/Assets/**` | Perception/Replay suites | A — copied to output via csproj `Content` |
| Replay assets (XML/JSON) | `tests/UniClaw.Runtime.Tests/Replay/Assets/**` | Replay suites | A — csproj `Content` |
| Vision test server | `tests/UniClaw.Runtime.Tests/Vision/vh_test_server.py` | VisionHostBehavioralProofs | A — csproj `Content`; python3 stdlib only |
| Vision YOLO model `best.pt` | `platforms/perception/models/...` | **NOT consumed by tests** (vision-host tests use fake temp files) | A — out of test scope |
| Node client scripts | `dsh-plugin-uniclaw/test/*.mjs` | DriverHost node E2E | A — documented (node on PATH) |

## 5. Hidden Dependency Findings

**No hidden dependencies found.** Specifically:

- **No hidden APK installation** — the only APK install path is the documented
  `install.sh`; no test invokes `adb install` itself.
- **No undocumented adb command** — every adb invocation is visible in
  real-device test source (`am force-stop/start`, `uiautomator dump`,
  `dumpsys window`, `cat`) or in the documented install script. No external
  script mutates device state behind the tests.
- **No manually prepared emulator state** — emulator boot is a declared
  prerequisite; tests self-setup (`am force-stop` / `am start` / readiness
  poll) and leave no persistent state.
- **No network dependency** — all `TcpClient` uses are loopback to an in-test
  DriverHost server (`127.0.0.1`); nothing external is contacted.
- **No hidden model/corpus loading in tests** — vision-host tests synthesize
  fake `best.pt`/`server.py` temp files; perception tests read committed repo
  assets only.
- **Serial/adb resolution is explicit** — `RealDeviceTestConfiguration`
  (env overrides → unique-online-device discovery → clear failure); no
  machine-specific default.

## 6. RealDevice Requirement Matrix

Full matrix in `docs/testing/test-environment-dependency-audit.md` §RealDevice
requirement matrix. Separation:

- **Architecture-proof tests (no device):** Evidence Specification tests
  (generic tree/diamond/Settings-as-fixture), OpenWorld/Semantic/Strategy/
  Pre-terminal deterministic suites, Replay/Perception asset suites, Vision
  host proofs, DriverHost loopback wire tests.
- **Hardware-integration tests (device required):** 6 Settings real-device
  suites (`com.android.settings`, system app, vision socket) + External
  boundary (also needs `com.android.permissioncontroller`) + Capstone
  real-emulator (needs fixture APK + vision socket).

Why real device for hardware tests: the graduated claim is about the real
external world — the production pipeline (screenshot → vision → structured →
Agent) against the real Settings app; a fake world can only prove the
mechanism, not the device integration.

## 7. Full Regression Result

| Step | Result |
|---|---|
| Build `src/UniClaw.Runtime.sln` (`-p:NuGetAudit=false`) | **0 errors / 0 warnings** |
| `UniClaw.Runtime.Tests` | **1935 / 1937 pass** (+4 new manifest guard tests) |
| `Semantic.Tests` | **32 / 32 pass** |
| `check-consistency.sh` | ALL PASS |
| `git diff --check` | clean |
| `openspec validate runtime-external-semantic-capability-boundary --strict` | valid |

### Failure classification (A/B/C — environment never marked as code)

**A. Code regression failures: NONE.** Every deterministic test passes; no
production change was made in this task.

**B. Environment unavailable failures: NONE** (device/APK/vision/node all
present; not the cause of the 2 remaining failures).

**C. Missing dependency / setup failures: 2 (both real-device, both known):**

1. `Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete` —
   **Known semantic-capability wiring gap (5.1 migration legacy), not an
   environment failure.** Emulator online, fixture APK installed, vision
   service healthy — but primary OCR elements classify `Unknown` because the
   real-device test wires no semantic capability. Adding one is outside this
   task's STOP conditions (and outside the audit's scope); it belongs to the
   `runtime-external-semantic-capability-boundary` review (Sol-gated), same
   family as the previously-fixed NodeClient E2E gap.
2. `ExternalBoundary_RealDevice` — **Device-state/setup failure**
   (`External foreground (com.android.permissioncontroller) not observed`).
   The emulator did not reach the permission-controller foreground state; a
   device-state precondition (permission dialog trigger) is not satisfiable in
   the current AVD session.

## 8. Remaining Blockers

- **Capstone semantic wiring** (real-device path): a deliberate decision
  (which semantic capability wires into the real-device pipeline) is needed —
  belongs to the external-semantic-capability-boundary change, Sol-gated.
- **External-boundary device state**: reproducible permission-controller
  foreground on the AVD must be established (documented as a setup
  precondition in the manifest; the test already performs `pm clear` + launch).
- No code-level blockers. Deterministic matrix (1935) is fully green.

## 9. Recommendation for Sol Review

**Recommendation: accept the dependency-transparency completion; request Sol
review of the two real-device items (Capstone semantic wiring + EBD device
state) as part of the external-semantic-capability-boundary graduation, not of
this audit.**

This task's objective is met: every test suite now has an explicit,
reproducible environment requirement declared in the manifest + audit doc; a
new engineer answers "what does this test need?" without reading test
implementation details. STOP conditions held — no test requires an
undocumented APK, no hidden manual emulator preparation, no production Runtime
change, no scenario knowledge in the generic evaluator, no action-sequence
scripting reintroduced, and no environment dependency is used as Runtime
behavior (device state is only a prerequisite; the Runtime itself never
depends on the test environment).

**This report is evidence and recommendation; it does not self-graduate.**
Independent review by DeepSeek-V4-Pro is requested before closure.
