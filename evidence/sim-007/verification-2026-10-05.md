# SIM-007 Verification — 2026-10-05

## 做了什么

验证 SIM-007 的 B+C 决策：PERC 专有载体保留局部行为 expectations 作为描述性记录，改用 `goalEvaluationRealization=not-applicable`，删除 certification；工具和测试对该分层 fail-closed。

## 验证结果

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `python3 -m json.tool scenarios/schema.json`; schema 校验 28 个场景；`python3 tools/validate-testset-manifests.py` | schema、manifest 合法 | PASS：28/28；3/3 manifests | 命令输出 |
| CONTRACT | `python3 tools/scenario_certify.py --check` | golden 条目认证有效，descriptive 条目不报缺认证 | PASS：28 files、0 violations；20 certified + 8 descriptive | 命令输出 |
| DETERMINISTIC | `dotnet test tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj --no-restore` | 场景载体与执法测试通过 | PASS：188/188 | `tests/UniClaw.Simulation.Tests/TestResults/sim007-sim008-final.trx` |
| SCENARIO | `python3 tools/scenario-coverage.py --trx tests/UniClaw.Simulation.Tests/TestResults/sim007-sim008-final.trx` | 场景映射、状态真值和适用认证通过 | PASS：28/28；20 certified + 8 descriptive | 命令输出 |
| SCENARIO | `python3 tools/verify-change SIM-007 --scope quick --no-snapshot` | 首批 7 场景 quick 通过，PERC caveat 如实输出 | PASS：7/7；`FINAL_STATUS=PASS`；`LIVE=SKIPPED` | 命令输出 |
| CONTRACT | `python3 tools/verify-change SIM-007 --self-test` | 工具契约完整 | PASS：T1–T21 | 命令输出 |

未执行 live/device 验证；本 Change 只覆盖仿真与声明分层。
