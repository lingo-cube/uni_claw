# PNL-006 — 全链路单 run 测试报告 · 首份真实产物

输入：PNL-003 的真实 Android Settings run fixture
（`evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844/`，
emulator-5556 / API 35 / glm-5.3-flash / 2 次 tap 效果 / Completed）。

产物：

- `run-20261003-075727-844/report.json` — `uniclaw.workspace.run-report.v1`
  canonical 报告（20177 bytes，L1-L4 四层，12 个 timeline 事件，23 条引用索引）
- `run-20261003-075727-844/report.md` — 同源事实的人读渲染

复现（字节级一致）：

```text
python3 tools/gen-run-report.py \
  --run-dir evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844 \
  --check --expect-dir evidence/pnl-006/run-20261003-075727-844
```

注意：`--check` 需从仓库根目录以相同相对路径调用（报告 envelope 记录调用者
原样路径以保证字节确定性）。

## 报告中两处「降级」的定性（历史局限，非设计缺口）

- **UniAgent 降级**：旧 fixture 早于「每次咨询后即时落盘
  `consultations.json`」的 HostRunner 机制；新 run 该分区应为完整。
- **Runtime Host 降级**：`.runtime-runs` 事件库在 Runtime 进程工作目录，
  不随 run 目录落盘，生成器当前输入不含它；P2 挂接时将其并入输入（或由
  Host 在 finalize 时直接产出报告），该分区即为完整。

预期：P2 之后的新 run 报告中，这两处降级不应再出现；若再出现即回归。
