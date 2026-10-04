# PNL-004 continuation verification — 2026-10-04

## Result

`WI-PNL004-006` 的实现验收已闭合。PNL-004 整体仍保持 open，因为真实 Runtime/Host transport 和真实设备发起链路尚未在当前环境中具备可执行证据。

## Verification

```yaml
level: CONTRACT+INTEGRATION+ENVIRONMENT
method: npm test --prefix dsh/uniclaw-task-workbench; npm test --prefix web/uniclaw-workspace; python3 tools/validate-workspace-schemas.py; python3 tools/validate-testset-manifests.py; dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore; DSH_TEST_ADB_ENV=1 UNICLAW_ANDROID_DEVICE=emulator-5556 dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter FullyQualifiedName~AdbEnvironmentTests; UNICLAW_ANDROID_DEVICE=emulator-5556 tools/verify-live; curl http://127.0.0.1:3081/
expected: launch contract、catalog、namespace、partial recovery 和 Host launch context 通过；已登记的 p26_pixel 克隆模拟器可被配置 serial 选中并完成真实 Android 闭环；未认证 DSH shell 仍受边界保护；真实 Runtime endpoint 仍需单独部署后才允许页面 launch。
actual: DSH 63/63、Web 76/76、workspace schema 7/7、testset manifest 2/2、Host 142/142 通过；启动配置 `p26_pixel` 产生 `emulator-5556`，API 35，生效窗口 1080x1920，ADB-002 真实 Wi-Fi toggle 1/1 通过；`tools/verify-live` 的 HostLiveFull 1/1、TypedLiveChain 1/1、LiveCoordinateGate 2/2 全部 PASS；3081 未带浏览器认证上下文的 shell 返回 HTTP 401；仓库仍没有已部署的 Host-facing Runtime HTTP service。
evidence: docs/agents/test-emulator.md; tools/android/start-test-device.sh; tools/android/device-capabilities.yaml; dsh/uniclaw-task-workbench/tests/; web/uniclaw-workspace/tests/; schemas/workspace/; testsets/; tests/UniClaw.Host.Tests/; tests/UniClaw.Kernel.Tests/AdbEnvironmentTests.cs; evidence/PNL-004-runtime-adapter-audit.md; evidence/PNL-004-WI-PNL004-005.md
```

## Remaining gates

- `WI-PNL004-007`：将 Host/Runtime transport 在真实 composition root 部署，并证明 Runtime-owned `runId`、Product Session、Host Session 和恢复关系。
- `WI-PNL004-005`：设备配置和真实 Host live gates 已通过；仍需在 Host/Runtime transport 部署后，从带认证的 3083 页面发起任务，验证 DSH 与独立 Web 两个入口以及 configured/generated/observed/derived 元信息。

本次验证没有把测试替身、未认证 shell 或已有只读 fixture 当作真实 launch 成功。
