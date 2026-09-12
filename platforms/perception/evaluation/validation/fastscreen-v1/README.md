# FastScreen Validation Set — FSV-001 (WI-1)

业务验证集，用于 FastScreen (ScreenParser) 集成/替代 A/B 实验的感知管线评测。
本目录全部内容为新采集资产；未修改仓库任何既有文件。

## Provenance

- **设备**: Android Emulator，AVD `p26_pixel`（`emulator-5554`），
  Android 15 (API 35, AOSP build for arm64)，1080×2400 @420dpi。
- **采集工具**: `collect.py`（本目录）；本数据集所有帧均由 `collect.py snap`
  产生，序列可由 `collect.py replay` 回放（见下）。
- **采集方式**: 仅 adb + 系统自带应用（Settings 系、SystemUI / QuickSettings /
  Settings Search / 系统通知）。`adb exec-out screencap -p` 截图，
  `adb shell uiautomator dump` 存 a11y 树；未安装任何应用。
- **帧命名**: 内容寻址 — `sha256(png bytes)` 前 12 hex；PNG 去重后同一
  frameId 可被多个序列引用（复用不重复存）。
- **采集时间**: `manifest.json` 的 `collectedUtc`。

## 采集前置（收集时已执行，replay 需要）

```
adb shell svc power stayon true
adb shell wm dismiss-keyguard
adb shell settings put global window_animation_scale 0
adb shell settings put global transition_animation_scale 0
adb shell settings put global animator_duration_scale 0
adb shell settings put global development_settings_enabled 1   # Developer options 可见性
```

## 分层（manifest.byStratum）

| stratum | 说明 |
|---|---|
| list | Settings 主列表 / 子列表（Wi-Fi 已知网络、蓝牙、应用列表） |
| sidebar | sidebar_substitute：Settings 两级返回结构（带 back 的深层页），
  真实侧栏不可用（AOSP Settings 无 drawer），meta.note 标注 sidebar_substitute |
| settings | 各设置页（Display/Battery/Storage/Sound/Date&time/System…） |
| dialog | 真对话框：Factory reset 确认（bottom-sheet）、径向时间选择器、
  QS Bluetooth 子面板（SystemUIDialog）、Apps 溢出菜单弹窗 |
| scrollable | 长列表滚动中途（Apps / Developer options 中段、深段） |
| dense | Developer options 顶部、Apps 列表深段、System apps 列表 |
| text-heavy | About phone、Legal & regulatory、Legal information 长文、搜索无结果页 |
| icon-heavy | 通知栏、完整快速设置面板（含暗色变体） |

暗色帧 ≥3（meta.note / label 含 `dark`）：Settings 首页、Wi-Fi 页、About
phone、QS 遮罩。overlay 帧 ≥2（meta.note 含 `overlay`）：Factory reset
确认对话框、时间选择器、QS Bluetooth 面板、Apps 溢出菜单。

## 序列（sequences/，8 个）

| seq | kind | 内容 |
|---|---|---|
| seq-scroll-apps | scroll | Apps 列表顶部 → 滚动中途 |
| seq-scroll-devopts | scroll | Developer options 顶部 → 滚动中途 |
| seq-click-connecteddevices | click | Settings 首页 → Connected devices |
| seq-click-network | click | Settings 首页 → Network & internet |
| seq-toggle-stayawake | toggle | Developer options “Stay awake” 开→关（含状态帧） |
| seq-transition-system-reset | transition | 首页 → System → Reset options → back → back |
| seq-dialog-factoryreset | dialog | Reset options → Factory reset 确认对话框 → 返回 |
| seq-notification | custom | 系统通知 → 通知栏 → 完整 QS → 收起遮罩 |

序列结构：`{sequenceId, kind, setup?, frames:[...], actions:[{from,to,action,params}], note}`。
`setup` 为到达首帧前的前置动作（扩展字段，见 README 备注）；`actions` 为帧间动作，
action 词表与 `collect.py` 的 `run_action` 一致，故可回放。

## 已知限制

1. **状态栏时钟变化**: 同屏多次采集因时钟/通知徽标变化会产生不同 hash——
   这符合内容寻址设计；序列内同页前后帧因此可能为不同 frameId。
2. **uiautomator 与可见 UI 差异**: a11y 树可能包含布局中不可见节点 / 缺失部分
   装饰性控件（如纯图形图标无 text/desc）；对 Sheet 对话框与 QS 面板，dump
   覆盖的是活动窗口。解析时以可见性过滤为准。
3. **About phone 页动画**: “About phone” 顶部存在持续动画，uiautomator 偶发
   `could not get idle state` 失败（`collect.py` 已做 rm+重试）；极端情况下
   产出 NO-XML 帧（如 `about-phone-dark`，已补录 XML 或记录）。重试间隔较长。
4. **Developer options 需前置开启**: `development_settings_enabled=1`
   （收集时已设置；replay 需要同一前置，见上）。
5. **该构建无 “Default USB configuration” / Force stop 确认框 / Add-network
   对话框**: Developer options 顶部页面与 AOSP 15 重组，对话框入口与文档
   描述不同；已用等效真实对话框替代（时间选择器、QS 面板等），并在本文档
   记录此差异。Force stop 点击不弹确认框（直接生效），故未采集该对话框。
6. **QS 遮罩下的 Settings 深页**: `am start` intent 在 Settings 任务栈
   存在时可能被投递到当前栈顶（“warning: activity not started…”），
   回放脚本以序列 setup 中的具体动作复现真实导航路径。
7. **replay 内容差异**: 回放按 manifest/sequences 重走导航+截图，帧 hash
   不同（时间性内容差异允许）；replay 输出到 `replays/`，不覆盖采集集。
8. **模拟器噪音**: 偶发屏幕超时/动画竞争导致单次 dump 阻塞较长,
   `collect.py` 内部均有超时与重试；批量捕获建议逐帧调用。

## 校验

```
python3 collect.py validate   # PNG 尺寸/三元组完整性/序列引用/manifest 一致性
python3 collect.py stats      # 分帧计数
```

## 最终统计（manifest.json 为准）

- 帧总数: 39（全部 PNG+XML+meta 三件套；2 帧 XML 为事后补录）
- byStratum: list 7 · sidebar 3 · settings 10 · dialog 3 · scrollable 3 ·
  dense 4 · text-heavy 5 · icon-heavy 4
- 序列: 8（scroll×2, click×2, toggle×1, transition×1, dialog×1, custom×1）
- 暗色帧: 4（home / wifi / about / QS）
- overlay 帧: 4（factory reset 确认框 / 时间选择器 / QS Bluetooth 面板 /
  Apps 溢出菜单）
- 设备: p26_pixel @ emulator-5554, 1080×2400 @420dpi, Android 15 (API 35)