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

## 报告中两处「降级」的定性（历史局限；P2 已于 PNL-011 落地）

- **UniAgent 降级**：旧 fixture 早于「每次咨询后即时落盘
  `consultations.json`」的 HostRunner 机制；新 run 该分区应为完整。
- **Runtime Host 降级**：旧 fixture 无 `runtime-run-events.json` 导出与
  `metadata.requirement`。PNL-011 起，RuntimeHttpServer finalize 会写
  metadata 投影（含需求原文）、导出 runtime 事件并自动产出报告——新 run
  的 host 分区应为完整、①需求区应有原文。

回归判据（PNL-011 起生效）：新 run 的报告若再出现这两处降级，或①需求区
显示「未采集」，即为回归。
