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

## 后续

- PER-008：管道显式化 + 可配置化（待 grill）
- OPT-001+：工程修复 / 同权重推理后端对照 / OCR 模型对照 / 融合改进
  （候选调研：`docs/analysis/per-005-algorithm-library-options.md`）
