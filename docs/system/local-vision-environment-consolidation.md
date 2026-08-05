# UniClaw Local Vision 运行环境固化方案

> 日期：2026-08-04 | 状态：已完成 | 关联 memory：[[local-vision-runtime]] [[default-yolo-model]]

## 目标

将 UniClaw local vision 的 Python 运行环境从零散的框架 Python 3.11 安装固化为可复现的 `.venv-local-vision/`，并确认 Deki-Yolo（`android_ui_detection_yolov8`）为唯一默认 YOLO 模型。

## 变更摘要

| 组件 | 变更前 | 变更后 |
|------|--------|--------|
| Python 环境 | 框架 Python 3.11 (`/Library/Frameworks/...`) | `.venv-local-vision/`（Python 3.11.9） |
| pip 安装 | `python3.11 -m pip`（易装到 3.9） | `.venv-local-vision/bin/python -m pip` |
| YOLO 模型 | `server.py` 已用 Deki-Yolo，但 `analyze.py` 默认 `yolo11n.pt`（COCO） | 统一为 `android_ui_detection_yolov8/best.pt` |
| requirements.txt | 11 行，缺 torch/onnxruntime，版本范围宽松 | 16 行 exact pins，覆盖全部运行时依赖 |
| uvicorn 启动 | 隐式依赖 PATH 上的框架 uvicorn | `UNICLAW_UVICORN_PATH` 显式指向 venv |
| 手动启动命令 | `cd tools/local_vision && python3 server.py &`（无效——server.py 无 `__main__`） | `.venv-local-vision/bin/uvicorn tools.local_vision.server:app --app-dir $REPO --uds /tmp/uniclaw-vision.sock &` |
| settings.local.json | 8 条框架路径 + 4 条失效 `.venv/bin/` 条目 | 全部改为 `.venv-local-vision/bin/` |

## Deki-Yolo 模型规格

- **本地路径**：`artifacts/local-vision/models/android_ui_detection_yolov8/best.pt`（6.2 MB）
- **来源**：HuggingFace `orasul/deki-yolo`（经 7890 代理下载）
- **标签**（21 类 Android UI 元素）：BackgroundImage, Bottom_Navigation, Card, CheckBox, CheckedTextView, Drawer, EditText, Icon, Image, Map, Modal, Multi_Tab, PageIndicator, Remember, Spinner, Switch, Text, TextButton, Toolbar, UpperTaskBar
- **配置位置**：
  - `server.py:38` — `UNICLAW_YOLO_MODEL` env → `artifacts/local-vision/models/android_ui_detection_yolov8/best.pt`
  - `analyze.py:21` — `--yolo-model` default = `artifacts/local-vision/models/android_ui_detection_yolov8/best.pt`
  - `HostCommands.cs:710-711` — `Path.Combine(repoRoot, "artifacts", "local-vision", "models", "android_ui_detection_yolov8", "best.pt")`

## 运行环境规格

- **Venv**：`.venv-local-vision/`（Python 3.11.9，Intel macOS x86_64）
- **关键 pinned 版本**：torch 2.2.2, torchvision 0.17.2, ultralytics 8.4.115, opencv 4.10.0.84, rapidocr-onnxruntime 1.4.4, onnxruntime 1.23.2, paddleocr 2.10.0, fastapi 0.141.1, uvicorn 0.52.1
- **依赖清单**：[tools/local_vision/requirements.txt](../tools/local_vision/requirements.txt)
- **OCR 后端**：RapidOCR（ONNX Runtime），`UNICLAW_OCR_BACKEND=rapidocr`（默认）
- **模拟器**：AVD `uniclaw-lite-api35`，1080x1920 @ 420dpi

## 验收标准与验证结果

### AC-1：Venv 完整可用

**标准**：`.venv-local-vision/bin/python` 可导入全部运行时依赖（torch, torchvision, ultralytics, paddleocr, rapidocr_onnxruntime, onnxruntime, fastapi, uvicorn, PIL, numpy, cv2）

**结果**：✅ 通过。`ALL IMPORTS OK`

### AC-2：Deki-Yolo 为统一默认模型

**标准**：`server.py`、`analyze.py`、`HostCommands.cs` 均默认使用 `android_ui_detection_yolov8/best.pt`

**结果**：✅ 通过。三处均已确认/修改。

### AC-3：analyze CLI 端到端可运行

**标准**：`python -m tools.local_vision.analyze --image <screenshot> --ocr-backend rapidocr` 退出码 0，输出证据 JSON 含有效检测

**结果**：✅ 通过。Settings 首页截图 → 11 YOLO detections（toolbar/input/icon/text_button…）、16 OCR 文本行、11 candidates。

### AC-4：VenV uvicorn 可启动 server

**标准**：`.venv-local-vision/bin/uvicorn tools.local_vision.server:app --app-dir $REPO --uds /tmp/uniclaw-vision.sock` 正常启动，`/health` 返回 `{"status":"ok","warm":true}`

**结果**：✅ 通过。集成测试中由 `PythonVisionService` 自动拉起并完成 readiness check。

### AC-5：场景集成测试端到端通过基础设施层

**标准**：`UNICLAW_INTEGRATION_SCOPES=scenario-locate UNICLAW_INTEGRATION_PROVIDER=local` 的集成测试不走云端模型，完整运行 vision pipeline（YOLO + OCR + fusion → provider → engine → ADB → verify）

**结果**：✅ 基础设施层通过。测试运行 40s，2 actions / 4 steps / 2 scrolls 执行成功，providerId=local。场景判定 `pending_verification`（`successCriteriaSatisfied: false`）——这是场景逻辑层的已知问题，非本方案变更引入。

### AC-6：无路径泄漏

**标准**：代码库中所有框架 Python 3.11 绝对路径和失效 `.venv/bin/` 路径已替换为 `.venv-local-vision/bin/`

**结果**：✅ 通过。`settings.local.json` 全部 12 条路径已更新。

## 环境重建步骤

```bash
cd /Users/fran/Documents/Code/spacex/uni-claw

# 1. 创建 venv
uv venv --python /Library/Frameworks/Python.framework/Versions/3.11/bin/python3.11 .venv-local-vision

# 2. 安装依赖
uv pip install --python .venv-local-vision/bin/python -r tools/local_vision/requirements.txt

# 3. 验证
.venv-local-vision/bin/python -c "import torch, rapidocr_onnxruntime, fastapi, uvicorn; print('OK')"
.venv-local-vision/bin/python -m tools.local_vision.analyze --image artifacts/assets/screenshots/settings-home-api35-full-20260803.png --ocr-backend rapidocr
```

## 修改文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `tools/local_vision/requirements.txt` | 重写 | 16 行 exact pins，补 torch/torchvision/onnxruntime |
| `tools/local_vision/analyze.py:21` | 改默认值 | `yolo11n.pt` → `android_ui_detection_yolov8/best.pt` |
| `.claude/settings.local.json` | 路径替换 + 加 env 块 | 12 条路径更新 + `UNICLAW_UVICORN_PATH` env |
| `.claude/skills/host-test-runner/SKILL.md:87` | 修复启动命令 | 无效的 `python3 server.py &` → venv uvicorn |
| `.claude/…/memory/local-vision-runtime.md` | 重写 | 固化 venv 规范、启动命令、dep 版本 |
| `.claude/…/memory/default-yolo-model.md` | 更新 | 确认 Deki-Yolo 为规范模型，记录 2026-08-04 验证结果 |
| `docs/system/local-vision-environment-consolidation.md` | 新建 | 本文件 |

## 已知限制

1. **torch 2.2.2 不可升级**——Intel macOS x86_64 的最后发布版本（无新版 wheel）。迁移到 Apple Silicon 后方可升级。
2. **paddleocr 2.10 有内存泄漏**——当前通过 RapidOCR 规避（默认后端），paddleocr 仅保留做对比验证。升级到 paddleocr 3.x 需 API 迁移。
3. **`AdbVisionActionIntegrationTests` 不支持 local provider**——`CreateAnalyzer` 未传 `labelMappingPath`/`pythonClient`，仅 Sensenova 路径可用。需后续修复 `HostCompositionFactory`。
4. **历史遗留 uvicorn 进程**——多次手动测试后可能残留（PID 9981/9766/7558/14807 等），`pkill -f "tools.local_vision.server"` 清理。
