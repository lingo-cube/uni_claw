# UniClaw Perception Provider（platforms/perception）

> 感知服务 provider（Python / FastAPI / uvicorn）：截屏像素 → YOLO 检测 +
> OCR → fusion → 结构化 JSON。Capability Plane 观察侧 provider 进程，
> 非 Authority；Kernel C# 零依赖本树，交互只经 UDS/TCP 契约
> （`/v1/analyze_raw`、`/v1/analyze`、`/health`、`/version`）。

## Provenance（PER-007 迁移留痕）

- **Upstream base**: uni-agent 分支 `platforms/perception/`（blob 级物化，
  基线含 PER-005 时期的 dict 修复：`ocr/models/en_PP-OCRv4_dict.txt`
  94→95 行，对齐其 governance manifest 注册的 "95-char en rec dictionary"）。
- **迁移方式**: 选择性复用 + 重构（Human 裁决），非原样 vendor-drop。
- **Delta（相对 uni-agent 上游）**:
  1. 仅迁移运行面：`uniclaw_perception/` + `models/` + `ocr/models/` +
     `config/` + `requirements/runtime.txt`；
  2. **R1** `ocr/rapid.py::configure_ocr_models` 重写——governance
     manifest 解析 → config 直指（`config.ocr_rec_model` / `config.ocr_dict`，
     env `UNICLAW_OCR_MODEL` / `UNICLAW_OCR_DICT` 覆盖；en 受管 / zh 包默认
     / 其他语言 fail-closed 语义保留）；
  3. **R2** `health.py` + `server.py` lifespan——governance.runtime_snapshot
     机制 → 包内启动期冻结身份（G9–G11 语义保留；/version 丢弃无消费方的
     additive 字段 configId/configCompleteness/pipelineRevision/deploymentId）；
  4. **R3** 未迁移：cli / tools / tests / reports / training / evaluation /
     persistence.py / governance；
  5. `config.py` 增 OCR 模型路径字段（R1 配套）。
- **行为冻结**：推理核心（yolo / ocr 推理 / fusion / operators /
  preprocessing / remap / schema）字节级未动；契约（路由 / 响应 schema /
  失败分类）零变化——PER-007 基线对照验收（见
  `evidence/2026-09-12-per-007-*.md`）。

## 管道配置（PER-008）

- `config/pipeline.json`：默认管道（= 历史行为，行为冻结）；配置面 = fusion
  四 knob（`interactiveExtraLabels` / `promoteUnmatchedOcr` / `stabilize` /
  `maxOcrDistanceRatio`）+ impl 声明（须与 cfg 推导一致，fail-closed）。
- `config/pipeline-variants/*.json`：预声明变体（启动全量 lint）；请求经
  `X-Pipeline-Variant: <variantId>` 选择（只可选、不可携带配置内容；未知 →
  400）。示例变体：`promote-off`。
- 四层身份（`uniclaw_perception/identity.py`）：modelId / configId /
  pipelineRevision / deploymentId——响应 metadata additive 携带（变体各持
  configId/deploymentId）。
- 基准：`python bench/run_l2.py [--variant NAME] [--runs N] [--out FILE]`
  （L2 录屏推理：per-stage 分位计时 + 输出哈希双锚 + 身份引用）。
- stage DAG 声明见 `uniclaw_perception/pipeline.py::STAGES`（串行实现；
  detect/recognize 并行化是 OPT-001 增量）。

## 运行

```bash
# 环境（一次性）：venv + RapidOCR 最小集（跳过 paddle；被配置时启动 fail-loud）
bash tools/perception-env/setup.sh

# 起服务（cwd = 本目录）
cd platforms/perception
../../.perception/venv/bin/python -m uvicorn uniclaw_perception.server:app \
  --uds /tmp/uniclaw-vision.sock
```

ENVIRONMENT 验收（A4/A5）：`DSH_TEST_PERCEPTION_LIVE=1 dotnet test
--filter PerceptionLiveEnvironmentTests`（模拟器生命周期外部管理，见
`docs/agents/test-emulator.md`）。

## FastScreen 实验变体（FSV-001，opt-in，默认管道零影响）

- 变体：`X-Pipeline-Variant: fastscreen-integration`（YOLO/OCR 原样 +
  screenparse 救援 step）/ `fastscreen-replacement`（detect 换
  ScreenParser，OCR 留；`-mps` 同义 MPS 版）。A/B 互斥，lint fail-closed。
- **权重前置**：ScreenParser v2（YOLO11-L，55 类）**不入 git**，用变体前
  需自行下载（源 `huggingface.co/docling-project/ScreenParser` `main`
  分支 `best.pt`，153,259,543 B，sha256
  `dbcb4f583ccfdb8100a68e606525c247890a2de4c1a54b14741e0ee29ce0ab88`）落
  `models/yolo/screenparser_v2/best.pt`（或 env `UNICLAW_SCREENPARSE_MODEL`
  覆盖）。缺席时仅默认管道可跑（变体路径 fail-closed）。
- 结论与证据：NO_CHANGE（Human Gate 已裁决保持 opt-in）——
  `changes/FSV-001/state.md` + `evidence/2026-09-12-fsv-001-validation.md`
  + `evaluation/reports/fsv001(-v2)/`。
- 评测复跑：`bench/compare_arms.py --valset
  evaluation/validation/fastscreen-v1 --arms
  baseline,integration,replacement,ocr-off`（GT/验证集同目录）。

## 后续

- ~~PER-008：管道显式化 + 可配置化~~（已落地）
- ~~OPT-001：工程修复 / 计时 / MPS / scorer / GC / 并行~~（已闭环）
- FSV-002（挂起）：rescue 限定 interactive 类重测 + Android 域微调
  重评——重启条件见 `changes/FSV-001/state.md`（延迟预算 ≥900ms/帧 且
  免费微调 checkpoint 出现）
- M4 Slow 层验证（后续 Change 讨论中；素材：
  `evaluation/reports/fsv001/screenvlm-probe/`）
