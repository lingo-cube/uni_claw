# AGT-013 verification — Android Settings safety policy runtime contract

Date: 2026-10-06  
Change: `AGT-013`  
Level: `DETERMINISTIC + SCENARIO`

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `SettingsCoverageConfig.LoadDefault()` through `SettingsActionPolicyTests` | required profile loads a generated policy with a stable digest | PASS：7/7 policy tests | `tests/UniClaw.Host.Tests/SettingsActionPolicyTests.cs` |
| CONTRACT | missing required policy fixture test | `PROFILE_CONTRACT_NOT_READY`; no usable config | PASS | `SettingsActionPolicyTests.MissingRequiredPolicy_FailsClosed` |
| DETERMINISTIC | forbidden target / unknown toggle guard tests | reject before dispatch; no receipt | PASS | `SettingsActionPolicyTests.ForbiddenAndUnknownTargets_AreRejectedBeforeDispatch`; `GuardDecision_RejectsBeforeKernelDispatch` |
| DETERMINISTIC | satisfied and unsatisfied Wi-Fi toggle tests | satisfied state becomes `NO_ACTION`; unsatisfied targeted toggle is allowed | PASS | `SettingsActionPolicyTests.SatisfiedTargetedToggle_BecomesNoAction`; `UnsatisfiedTargetedToggle_IsAllowedOnce` |
| DETERMINISTIC | `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore` | Host and existing Settings coverage remain green | PASS：153/153 | command output |
| DETERMINISTIC | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore` | Runtime contract/assurance regression remains green | PASS：794/794 | command output |
| SCENARIO | `dotnet test tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj --no-restore` | simulation baseline remains green | PASS：188/188 | command output |
| DETERMINISTIC | `dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --no-restore` | DSH adapter/session contract remains green | PASS：132/132 | command output |
| CONTRACT | `dotnet build src/UniClaw.Host.Dsh/UniClaw.Host.Dsh.csproj --no-restore` | real DSH Host composition root builds | PASS：0 errors | command output |
| CONTRACT | `python3 tools/validate-testset-manifests.py`; `git diff --check` | testset contract and changed files are valid | PASS：3/3 manifests; diff clean | command output |

The live Android device gate was not run in this Change. The profile/runtime contract
is now ready for the three AGT-012 real tasks; real-model and emulator evidence remain
separate from this deterministic result.
