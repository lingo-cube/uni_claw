# PER-007 Evidence — 感知服务选择性迁移 + 重构

> Date: 2026-09-12 · level: A1/A2 DETERMINISTIC（基线对照 + fail-closed 实证）、
> A3/A4 ENVIRONMENT、A5 全量回归
> 环境: 同 PER-005（macOS arm64 · venv pin 原样 · p26_pixel / emulator-5554）

## A1 — 基线对照（迁移零行为漂移）

- 基线：迁移前 `.perception/provider` 树对 corpus 帧（golden-run-v1
  case-a-before，1080×1920 RGBA）的 `/v1/analyze_raw` 响应，sha256
  `b5b7aa9b…`（yolo=16 / ocr=11 / candidates=13）。
- 迁移后：`platforms/perception` 新树同帧响应，sha256 `98b5c7f8…`。
- **对拍结果（D4 口径）**：
  - `yolo` / `ocr` / `candidates` / `_diagnostics` / `summary` /
    `scrollHints` / `image`：**逐字节语义全等**；
  - `metadata`：schema / width / height / pipeline / **configHash / modelId**
    全等；唯一差异 = `metadata.models.yolo` 绝对路径（`.perception/provider`
    → `platforms/perception`，D4 预期内）；
  - 顶层键集合一致。
- sha256 不等仅由上述路径字符串贡献。

## A2 — fail-closed 语义保留（R1）

- paddle 被配置而环境缺 paddle：`A4_PaddleConfiguredButAbsent_
  StartupFailsLoud` GREEN（启动失败 + stderr 现场，无静默降级）。
- R1 模型缺失实证：`UNICLAW_OCR_MODEL=/nonexistent/model.onnx` 启动 →
  `RuntimeError: managed OCR rec model missing on disk: /nonexistent/
  model.onnx` → 进程 exit 3（fail-closed，不静默回退默认中文模型）。
- 相对导入修正记录：R1 首版 `from .config` 在 `ocr/rapid.py` 内错层
  （`uniclaw_perception.ocr.config` 不存在）→ `from ..config`（启动期即
  暴露，修复后 2s 健康）。

## A3/A4 — ENVIRONMENT（ProviderRoot = platforms/perception）

- `PerceptionLiveEnvironmentTests` **3/3 GREEN**（9s）：
  - A4 服务健康 + corpus 帧非空推理（`/version` 冻结身份：modelId 内容
    哈希 / configHash 与迁移前一致）；
  - A4 paddle fail-loud；
  - A5 模拟器现场全链：截屏 → 服务推理 → derived artifact →
    FastPerception → EvidenceLedger admitted 非空。

## A5 — 全量回归

- `dotnet test UniClaw.Kernel.slnx`：**Kernel 347/347 + Agent 17/17**
  GREEN（与 PER-005 基线完全一致，零破坏）。

## 交付物清单

- `platforms/perception/`（44 文件 / 14MB）：uniclaw_perception 包 + models
  （YOLO best.pt 6.2MB + OCR rec onnx 7.7MB + 修复后 95 行 dict）+ config +
  requirements/runtime.txt + README（provenance：upstream base / R1–R3 delta）。
- `tools/perception-env/setup.sh` v2：venv + pip only（物化步骤删除）。
- `PerceptionLiveEnvironmentTests` ProviderRoot 重指 + 树在场前置检查。
- `.gitignore`：+ `__pycache__/` / `*.pyc`（Python 树入仓配套）。
- `.perception/provider` 已退役（venv/cache 保留共用）。

## 重构手术记录（R1/R2 相对上游）

- R1 `ocr/rapid.py::configure_ocr_models`：governance manifest 解析 →
  config 直指（en 受管 + dict 必在 / zh 包默认 / 其他语言 RuntimeError）。
- R2 `health.py`：包内启动期冻结身份（capture_identity；G9–G11 语义保留），
  /version 丢弃无消费方的 additive 字段；`server.py` lifespan 同步替换
  governance snapshot 块。
- R3 未迁移：cli / tools / tests / reports / training / evaluation /
  persistence.py / governance。

## 遗留

- PER-008（管道显式化 + 可配置化）：已剥离建账，待 Human 补充设计细节后
  grill 定稿。
