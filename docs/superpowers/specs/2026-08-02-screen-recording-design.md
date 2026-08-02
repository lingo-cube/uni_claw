# macOS Emulator Screen Recording Design

> 状态: draft | 日期: 2026-08-02 | 优先级: P2（低）

## 1. 目标

在场景执行期间录制模拟器窗口视频，用于：
- 回放分析：出问题时回溯操作序列
- 证据保留：每次 run 附带视频证据
- CI 集成：无人值守时的唯一可视化输出

## 2. 约束

- 仅录制模拟器窗口，不录整个屏幕
- 不影响引擎性能（后台异步录制）
- macOS 原生方案（无需第三方依赖）
- 可配置开关（默认关闭）

## 3. 技术方案

### 3.1 macOS AVFoundation 命令行

```bash
# 获取模拟器窗口 ID
window_id=$(osascript -e '
  tell application "System Events"
    set emulator to first process whose name contains "qemu"
    return id of first window of emulator
  end tell')

# 录制指定窗口（mp4, h264）
screencapture -v -l "$window_id" output.mp4

# 或使用 avfoundation 设备（更高性能）
ffmpeg -f avfoundation \
  -capture_cursor 1 \
  -capture_mouse_clicks 1 \
  -i "1:none" \
  -vf "crop=iw:ih:window_x:window_y" \
  output.mp4
```

### 3.2 Swift Script（精确控制）

```swift
// record_emulator.swift — 使用 ScreenCaptureKit (macOS 13+)
import ScreenCaptureKit
import AVFoundation

let filter = SCContentFilter(desktopIndependentWindow: emulatorWindow)
let config = SCStreamConfiguration()
config.width = emulatorWindow.frame.width
config.height = emulatorWindow.frame.height
config.queueDepth = 6

let stream = SCStream(filter: filter, configuration: config, delegate: nil)
try await stream.addStreamOutput(output, sampleHandlerQueue: .main)
try await stream.startCapture()
```

## 4. 集成方式

### 4.1 Host 入口

```csharp
// HostCommands.cs 或 HostCompositionFactory
public async Task RunWithRecordingAsync(HostCommandOptions options)
{
    var record = options.Record == true
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNICLAW_RECORD"));

    Process? recorder = null;
    string? videoPath = null;

    if (record)
    {
        var windowId = await AdbEmulatorResolver.GetWindowIdAsync(options.Serial);
        videoPath = Path.Combine(options.OutputPath, "recording.mp4");
        recorder = StartScreenCapture(windowId, videoPath);
    }

    try
    {
        await RunScenarioAsync(options);
    }
    finally
    {
        if (recorder is not null)
        {
            recorder.Kill(entireProcessTree: true);
            await recorder.WaitForExitAsync();
        }
    }
}
```

### 4.2 窗口定位

```bash
# AppleScript: 根据 ADB serial 找到模拟器窗口
adb -s emulator-5554 emu avd name  # → "Pixel_6_Pro_API_35"
osascript -e "
  tell application \"System Events\"
    set emuName to \"Pixel_6_Pro_API_35\"
    return id of first window of process \"qemu-system-aarch64\" whose name contains emuName
  end tell"
```

### 4.3 录制命令

```bash
# 最优方案：screencapture 直接录窗口（macOS 内置）
screencapture -v -l <window_id> -D 1 <output_path> &
# -v: video mode, -l: window ID, -D 1: 1 fps display capture

# 停止：kill 进程
```

## 5. 文件组织

```
artifacts/runs/integration/<scope>/<runId>/
├── steps/
├── trace/
├── safety-decisions.jsonl
├── recording.mp4              ← 新增
└── result.json
```

## 6. 配置

```
# 环境变量
UNICLAW_RECORD=1              # 启用录制
UNICLAW_RECORD_FPS=10         # 帧率（默认 10）
UNICLAW_RECORD_CODEC=h264     # 编码（默认 h264）

# CLI 参数
dotnet run -- run --scenario <path> --record
```

## 7. 性能影响

| 指标 | 预估值 |
|------|--------|
| CPU 占用 | ~5-8%（screencapture） |
| 磁盘占用 | ~2-5 MB/min（1080p, 10fps, h264） |
| 引擎延迟 | 0ms（完全异步） |

## 8. 实施计划

| Phase | 内容 | 优先级 |
|-------|------|--------|
| P1 | `screencapture` + AppleScript 窗口定位 | P2 |
| P2 | Swift ScreenCaptureKit 方案（macOS 13+） | P3 |
| P3 | 帧内叠加 step number / action overlay | P4 |

## 9. 风险

| 风险 | 缓解 |
|------|------|
| 模拟器窗口名不固定 | 用 ADB avd name + fuzzy match |
| screencapture 不支持窗口模式 | fallback 到全屏录制 |
| 磁盘空间不足 | 限制录制时长上限（= scenario maxDuration） |
