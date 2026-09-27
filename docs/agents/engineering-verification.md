# Engineering Verification Consolidation

## Entries

- `tools/verify-live`：只读 live wrapper。无显式设备时复用
  `tools/android/start-test-device.sh` 的临时 AVD；使用现有 selector、capability
  manifest 和 HostLiveFull、TypedLiveChain、LiveCoordinateGate。结束时复用 stop
  script。环境不可用输出 `ENVIRONMENT_UNAVAILABLE`，gate 失败输出 `FAIL`。
- `tools/verify-change <CHANGE-ID>`：默认 check-only，编排 diff check、现有
  architecture tripwire、Simulation、full solution、`scenario_certify.py --check`
  和 coverage。`--live` 才调用 live wrapper，工具不修改 state/evidence/INDEX，
  不自动 re-certify 或关闭 change。
- `--self-test`：执行 T1-T14 的契约自检，验证设备选择、tripwire、分类和只读约束。

## Classification

`BEHAVIOR_REGRESSION` 表示 Simulation 失败或 expectationsDigest 发生变化。
仅 certification runtimeSourceHash 过期时输出 `CERTIFICATION_STALE_ONLY`，并提示
使用既有合法认证命令；工具不会自动执行该命令。设备不可用保持
`ENVIRONMENT_UNAVAILABLE`，不折算为产品失败。
