# PER-008 Evidence — 管道显式化 + 可配置化 + 四层身份 + L2 基准

> Date: 2026-09-12 · level: DETERMINISTIC（A1–A5）+ 全量回归（A6）
> 环境: 同 PER-007（macOS arm64 · venv pin 原样 · 服务经 UDS）

## A1 — 默认行为 = PER-007 基线

- 服务实测：默认管道下 corpus 帧（golden-run-v1 case-a-before）响应与
  PER-007 迁移基线（`98b5c7f8…` 口径）**语义数组逐项全等**（yolo/ocr/
  candidates/_diagnostics/summary/scrollHints/image）；metadata 既有字段
  （schema/models/configHash/modelId）不变，新增身份字段
  （configId/pipelineRevision/deploymentId/pipelineVariant="default"）。
- 接线证明（`tests/test_pipeline_config.py::TestWiring`）：默认配置的 fuse
  kwargs == server.py 历史硬编码值（promote=True / stabilize=True /
  ratio=0.055 / labels⊇{text_block,text}）——**不依赖帧内容运气**（该帧
  恰无 unmatched OCR，行为对照不会显差异；接线由 kwargs 捕获证明）。

## A2 — 变体选择与身份独立

- `X-Pipeline-Variant: promote-off` → 200，metadata 带该变体独立
  configId/deploymentId/pipelineVariant；yolo/ocr 不受 fuse 变体影响。
- 接线证明：变体参数（promote=False / ratio=0.02 / labels 去掉 text）
  经 kwargs 捕获确认到达 `fuse_evidence`。
- 未声明变体 → **400**（`unknown pipeline variant`，不静默回退默认）。

## A3 — fail-closed

- `tests/test_pipeline_config.py::TestFailClosed` 5 用例 GREEN：未知 knob /
  未知 detect impl / 未知 recognize impl / schemaVersion≠1 拒绝；
  recognize impl 与 cfg 推导冲突拒绝（单一真相源）。
- 服务级：坏 `UNICLAW_OCR_MODEL` → RuntimeError → exit 3（R1 不回归）。

## A4 — 既有 fail-loud 不回归

- `PerceptionLiveEnvironmentTests.A4` **2/2 GREEN**（重构后服务：/version
  健康 + corpus 帧非空推理 + paddle 配置启动 fail-loud）。

## A5 — L2 基准（bench/run_l2.py）

- default：单输出哈希（确定性）、yolo p50=34.9ms / p95=45.9ms、total
  median=371.7ms（n=10 达 p50/p95 守门线）；identity
  deploy:ec03b7a154b95…
- promote-off 变体：独立 deploy:5cba158a8a62d…，同样单哈希守门全过。
- 报告含：四层身份 / 输入 sha256 / per-stage 分位 + 守门注释 / 输出哈希。

## A6 — 全量回归

- `dotnet test UniClaw.Kernel.slnx`：**Kernel 347/347 + Agent 17/17**
  GREEN（C# 侧零改动——变体选择/身份均为 provider 侧 additive）。

## 交付物

- `uniclaw_perception/pipeline.py`（pydantic schema + 变体 + lint + STAGES
  DAG 声明）+ `identity.py`（四层身份三纯函数，自 uni-agent governance 平移，
  canonical hash 内联；BEHAVIOR_MODULES 增 pipeline.py、排除 identity.py 自身）
- `config/pipeline.json` + `config/pipeline-variants/promote-off.json`
- `server.py`：管道注册（lifespan 全量 lint）+ fusion 四 knob 接配置 +
  `X-Pipeline-Variant` + metadata 身份字段
- `bench/run_l2.py` + `tests/test_pipeline_config.py`（7 用例）+
  `requirements/dev.txt`（pytest）

## 实现期修正（留痕）

- FuseParams 需 camelCase 别名 + populate_by_name（pydantic v2）。
- 变体加载需剥离顶层 variantId 再下传 config（extra=forbid 拒绝未知键）。
- bench 默认帧路径层级修正（platforms/perception → repo 根需 parent.parent）。

## Defer 账（显式重开条件）

- 热切换（file-watch/信号/端点）：长生命周期生产服务 + 零重启配置轮换
  buyer；届时 watchfiles 已在 venv（uvicorn[standard] 带入）。
- Ray/Triton/BentoML：多节点 / 多副本 / 集群采样 / 队列积压。
- detect/recognize 并行化 + 替代后端 impl 注册：OPT-001。
