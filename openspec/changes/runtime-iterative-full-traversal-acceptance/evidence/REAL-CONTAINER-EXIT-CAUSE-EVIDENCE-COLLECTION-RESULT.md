# PROJECT_LEADER_REAL_CONTAINER_EXIT_CAUSE_EVIDENCE_COLLECTION_RESULT

> Gate: `PROJECT_LEADER_REAL_CONTAINER_EXIT_CAUSE_EVIDENCE_COLLECTION_GATE` · **采证 ONLY（零生产行为修改）** · Phase 2.6 STOPPED
> 目标：复现 Display child 底部探索中"无动作间隔 → Settings Root"并捕获可判定证据链。
> 结论：**受控 5 次同型 campaign 全部未复现（0/5）；唯一历史实例 r5 证据链完整但缺设备输入/生命周期/录屏证据；
> 分类 = I. UNKNOWN（沿用 §9 术语：历史复现实例存在但证据不足；受控现状 = H. NOT_REPRODUCED）。
> 不实施猜测性 repair；采证管道已就绪，留待下次复现实例。**

## 1. Reproduction Count

| run | 类型 | frames | left-container | terminal |
|---|---|---|---|---|
| r5（历史） | 复现实例 | 19 | **1（seq28, page 'Settings'）** | quiescence exhausted / left container |
| runA | 受控同型 | 21 | 0 | exhaustion at 31 → Unknown·completeness |
| runB | 受控同型 | 13 | 0 | exhaustion → Unknown·completeness |
| runC | 受控同型 | 21 | 0 | exhaustion → Unknown·completeness |
| runD | 受控同型 | 21 | 0 | exhaustion → Unknown·completeness |
| runE | 受控同型 | 13 | 0 | exhaustion → Unknown·completeness |

- 受控复现率 **0/5**；总 1/6 → 表现 = **ONE-OFF / INTERMITTENT**（无法进一步区分）。
- 所有受控 run 的 child 探索均**正常到达 positive exhaustion**（runA/C/D seq…31；runB/E 更早）——
  底部滚动路径本身在 0/5 中未触发返回。

## 2. 同步 Action/Observation/Device Timeline（runA 示例，统一时间可用）

`P26_TIMESTAMPS`（validation-side 观测 tap）逐观测记录 `{observationSeq, wallClock(UTC), runRelativeMillis}`；
logcat `-v threadtime`（设备钟≈UTC，RUN offset 记录）；screenrecord 分段。示例（runA）：

```
seq1  = 18:25:42.664  → launch
seq22 = 18:26:48.475  → Display child 首观测（TAP 后）
seq25 = 18:26:57.701  → child 稳定确认
seq27 = 18:27:04.300  → child 滚动后
seq28 = 18:27:07.072  → attempt2（本 run 无 exit）
seq31 = 18:27:16.504  → child exhaustion
```

`ScrollForward dispatch ±2s` 可查（dispatch 时刻 = 前一 observation→下一 observation 之间的 swipe；logcat
WindowManager 过渡事件可佐证）；`seq27→seq28` 窗口可查询（本 run 无 exit，窗口内仅常规滚动 settle）。

## 3. AssetRef 清单（含 MISSING）

| AssetRef | type | run/seq | producer | path | status |
|---|---|---|---|---|---|
| A-1 | STAGE_ARTIFACT | runA-E / 全部 | CampaignProgram | `/tmp/p26-exit-{runA..E}-stage.json` | ✓ |
| A-2 | RAW_FRAME | runA-E | CampaignProgram | `/tmp/p26-exit-{runA..E}-frames.json` | ✓ |
| A-3 | **TRACE_ARTIFACT（seq↔time）** | runA-E / 每 seq | 本 gate 新增观测 tap（validation-side）| `/tmp/p26-exit-{runA..E}-timestamps.json` | ✓（新增能力）|
| A-4 | LOGCAT | runA-E / 全程 | adb logcat | `/tmp/p26-exit-{runA..E}-logcat.txt` | ✓（0 FATAL/ANR/back 调用）|
| A-5 | SCREEN_RECORDING | runA-E | adb screenrecord 分段 | `/tmp/p26-exit-{runA..E}-screen-{1..}.mp4` | ⚠ **moov 缺失不可读**（驱动 kill 过早，录制未 finalize；已定位修复=让其自行到期/`--output-format mkv`）|
| A-6 | SCREENSHOT（exit 实例）| r5 / seq27→28 | n/a | — | **MISSING_ASSET: screenshot**（历史未采集）|
| A-7 | LOGCAT（exit 实例）| r5 | n/a | — | **MISSING_ASSET: logcat** |

AssetRef 字段（future Query Core 兼容）：assetId/assetType/runId/seq/relativeMillis/wallClock/producer/path +
（截图为 bounds/occurrence/evidence refs）。不伪造截图。

## 4. Input Causality Investigation（r5 历史实例 + 受控对照）

```
Runtime Intent   : Action-8 = ScrollForward（协议动作词汇 = Tap|SetSwitch|ScrollForward|Launch，无 Back/KeyEvent）
                   → A. RUNTIME_EXTRA_ACTION 由构造排除。
Adapter Dispatch : DeviceActionTranslator（L79-101）→ adb `input swipe 540,1680 → 540,720`（dur 由距离限速）
                   → B. ADAPTER_INPUT_TRANSLATION 无偏差证据。
Device Input     : r5 无 logcat（MISSING）；受控 5 run logcat 无 keyevent/back 调用（CoreBackPreview 仅窗口创建时
                   的 OnBackInvoked 注册，非调用）→ 无 F. EXTERNAL_INPUT 证据。
UI Transition    : r5 seq28 真实根页（structured search_action_bar + 完整根行）→ 确为容器外。
```

- **手势几何**：swipe x=540（中轴）、y 1680→720（70%→30%），距底缘 gesture-home 区 >720px、无边缘 →
  **C. GESTURE_NAVIGATION_COLLISION 几何排除**（`COORDINATE_PROXIMITY != CAUSE` 守则——我们不是"看到靠边"，
  是根本不在边）。
- **D. SETTINGS_APPLICATION_BEHAVIOR / E. ACTIVITY_LIFECYCLE_EVENT / G. ENVIRONMENT_INSTABILITY**：
  受控 0/5 无对应事件；r5 实例缺日志 → **均无证据，不猜测**。

## 5. 生命周期检查（受控 run 的 exit 等价窗口）

- 受控 5 run：**0 FATAL / 0 ANR / 0 back 调用 / 0 activity recreate**（logcat 扫描）。
- `LAST_DEVICE_STATE_BEFORE_EXIT`（r5，历史）：Display child 底窗（seq27 structured=Display 行）——无设备日志。
- `FIRST_DEVICE_STATE_AFTER_EXIT`（r5）：Settings root（seq28 structured 根标记+根行）。
- `FIRST_DEVICE_EVENT_CAPABLE_OF_CAUSING_EXIT`（r5）：**未捕获**（无 logcat/录屏）→ 保持 UNKNOWN，不猜。

## 6. Video/Screenshot Reality Proof

- 受控 run 未复现 exit → 无 T0–T5 序列可标（诚实记录：本次采集无 exit 视觉链）。
- r5 T0–T5 的视觉证据缺失（MISSING_ASSET）；仅 structured 文本证据链（前 gate 已建成）。
- `VIDEO_ASSET != RUNTIME_AUTHORITY`：本 gate 视频仅备查，不做权威。

## 7. 分类（gate §9 单分类）

| 候选 | 判定 | 依据 |
|---|---|---|
| A. RUNTIME_EXTRA_ACTION | ❌ 排除 | 脚本动作词汇无 Back；trace 仅 ScrollForward/Tap/Launch；无重复 dispatch |
| B. ADAPTER_INPUT_TRANSLATION | ❌ 无证据 | translator 仅产单条 up-swipe；坐标/时长确定 |
| C. GESTURE_NAVIGATION_COLLISION | ❌ 几何排除 | x=中轴、y 70%→30%，远离子系统手势区 |
| D. SETTINGS_APPLICATION_BEHAVIOR | ❌ 无证据 | 0/5 未复现；r5 无法佐证 app 自发返回 |
| E. ACTIVITY_LIFECYCLE_EVENT | ❌ 无证据 | 受控 logcat 0 事件；r5 无日志 |
| F. EXTERNAL_INPUT | ❌ 无证据 | 无外部注入证据 |
| G. ENVIRONMENT_INSTABILITY | 候选未证实 | emulator 曾有崩溃先例，但 r5 实例无日志可核 |
| H. NOT_REPRODUCED | **受控现状** | 0/5 |
| **I. UNKNOWN** | **主分类** | r5 实例真实存在但设备输入/生命周期/录屏证据缺失；不猜测修复 |

**FDP**：无运行时侧缺陷（意图=唯一合法 scroll；判定正确）。触发事件（若为真实系统/应用行为）未捕获。
**Owner**：未定（D/E/F/G 候选均无证据）—— 保持 EVIDENCE_COLLECTION。

## 8. 采证管道交付（本 gate 的新能力，未来复现即用）

1. **Validation-side 观测时间戳 tap**（`SettingsCampaignProgram` 的 ObservationTap + `P26_TIMESTAMPS` dump）：
   seq↔UTC↔run-relative 统一时间轴 —— trace≤>logcat≤>video 可关联。
2. **采证驱动**（`/tmp/p26-exit-evidence.sh`）：screenrecord 分段 + logcat + 时间锚点 + 逐 run 资产。
3. 已知缺陷（已登记）：screenrecord 被 kill 未 finalize（moov 缺失）→ 修复 = 让录制自然到期或
   `--output-format mkv`。

## 9. Debug Toolchain Buyer Gaps（§11 逐项，均为人工/脚本拼接）

| 能力 | 现状 |
|---|---|
| action ↔ observation correlation | 手工：stage trace（Step/Action）与 timestamps 靠顺序对齐 |
| trace time-range query | 手工 jq；`--around action-8 --window 2s` 查询不存在 |
| logcat time-range query | 手工 grep + 设备钟对齐（本环境设备钟≈UTC，已验证）|
| Trace causal tree | 手工剪枝（上一 gate §10）|
| AssetRef indexing / video timestamp linking / screenshot lookup / device-state lookup | 手工；`MISSING_ASSET` 机制已登记；README 级索引 |
| Evidence Packet generation | 手工组装（本文档）|

等价未来查询（未实现，仅登记）：`runtime-debug logs/trace causal/assets/device-state/packet <run> --around …`。

## 10. Next Human Gate / Phase 2.6

- **保留 nondeterministic incident**（r5 1 次、受控 0/5）；**不实施猜测性 repair**（§10 规则：I → EVIDENCE_COLLECTION 继续）。
- 下一次复现实例出现时：采证管道已就绪 → 补 3 项决定性证据 = **设备输入（InputDispatcher 注入）/ logcat
  lifecycle 时间窗 / 录屏 T0–T5**；届时按证据归入 D/E/F/G 或确认为系统异常（ENVIRONMENT_GATE）。
- **Phase 2.6 维持 STOPPED**；本 gate 采证完成，停止，等待 Human。

## 11. Boundary Declaration

零生产行为修改（仅 ValidationHarness 观测侧新增时间戳 tap，非 Runtime authority）；未改 Runtime /
completeness / page continuity / resolver / budget / retry / sleep / swipe 参数 / exhaustion 规则 /
Not set / Will never / ICON/OCR/Safety；未因一次 logcat 猜测实施 repair。