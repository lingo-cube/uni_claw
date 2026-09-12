# FSV-001 Plan — FastScreen Integration & Replacement Validation（HOW）

> Change State: changes/FSV-001/state.md（WHAT/WHY/ACCEPTANCE）。本文件只
> 回答 HOW：切片顺序、委派拆分、执行纪律。垂直切片：每步产出可验证
> 证据，不按层拆。

## 切片顺序

```text
S1 [parallel]  WI-1 验证集采集          WI-2 FastScreen provider+adapter+integration 变体
S2             WI-3 replacement impl（B1 变体）+ B2 bench 消融
S3 [parallel]  WI-4 GT 完成（a11y 候选 + Leader 校正）   WI-5 四臂 A/B scorer+指标+报告
S4             Leader Review/Verify + Human Gate 报告（evidence/ 落档）
```

## WI-1 验证集采集（subagent，独立）

- 前置：emulator-5554 在线（注册 AVD p26_pixel）。
- 采集 40±10 帧 + 8±2 序列，覆盖 state.md D5 分层；每帧存 PNG +
  uiautomator XML + frame meta（stratum/sequence/position/route）。
- 内容寻址命名（sha256 前 12 hex）；manifest.json + 可重放采集脚本 +
  README（provenance）。产出目录：
  `platforms/perception/evaluation/validation/fastscreen-v1/`。
- Acceptance：帧数/分层/序列数达标；每帧三件套齐全；脚本可重放。

## WI-2 FastScreen provider + adapter + Integration 变体（subagent）

- 新模块 `uniclaw_perception/screenparse/`：provider（模型加载缓存 +
  推理，imgsz=1280 conf=0.10 iou=0.10）+ adapter（55→canonical 映射表 +
  structural Optional Evidence）。
- pipeline.py：STAGES 增 `screenparse`（dependsOn detect+recognize，
  fuse 改依赖 screenparse 的合并池输出——默认变体下恒等）；
  PipelineConfig 增可选 `screenparse` 段；变体
  `config/pipeline-variants/fastscreen-integration.json`。
- server.py：stage 接线（rescue 合并池喂 fuse；`screenParse[]` additive；
  Server-Timing `screenparse` 分段；identity/configId 自动随变体）。
- pytest：映射表完备性（55 类全覆盖：映射或显式 structural）、确定性
  （同输入同输出）、默认管道字节等价回归（无变体头 = 现行为）。
- Acceptance：V1 回归绿 + V2 单测绿 + 变体路径在 3 个既有资产上可跑通。

## WI-3 Replacement impl + B2 消融（subagent，依赖 WI-2 的 provider/adapter）

- detect impl 注册表 + `screenparser`（device cpu/mps；weights/env 同
  WI-2）；变体 `fastscreen-replacement`（B1：yolo[] ← ScreenParser 适配
  输出，OCR 原样，fusion 原样）。
- B2 消融：bench 层 ocr_tokens=[] 注入（空 ocr[] + fusion 无文字路径），
  不动服务配置面；产出消融入口脚本。
- Acceptance：B1 变体在既有资产上跑通且响应 schema 与 baseline 同构；
  B2 消融可重放。

## WI-4 GT 完成（Leader 主导 + subagent 辅助）

- subagent：从 uiautomator XML 生成候选 GT（可见叶子元素 → gtClass +
  normalized bounds + text + interactive），生成 overlay 可视化
  （帧 + 编号框），产出候选 GT JSON。
- Leader：read_image 核对 ≥5 屏 calibration core 全框修正；其余抽样
  核对；记录 GT provenance 与已知偏差（a11y 与可见 UI 的系统性差异，
  如不可见节点/合并文本）。
- Acceptance：GT 覆盖全部帧；calibration core 有 Leader 修正记录。

## WI-5 四臂 A/B scorer + 指标 + 报告（subagent，依赖 WI-1..4）

- 臂：baseline / A(integration) / B1(replacement) / B2(ocr-off 消融)；
  同验证集、同 GT、同 device（主 CPU，辅 MPS）、同 scorer。
- 指标 = state.md D7 全集；matcher 沿用 matcher-greedy-v1 语义。
- 产出：`bench/compare_arms.py`（或等价）+ scorecards（内容寻址）+
  `evidence/2026-09-12-fsv-001-*.md`（四臂对照表 + 失败案例分析 +
  latency/RSS）。
- Acceptance：V3 全指标有值或显式 NOT_SCORABLE；V4 Runtime 兼容验证
  （C# envelope + LiveVisionStrategy 解析四臂响应零修改）。

## S4 Leader 收口

- Review（意图对齐/范围/边界/意外改动）→ Verify（V1–V5）→ 判定建议
  （INTEGRATE/PARTIAL_REPLACE/REPLACE/NO_CHANGE + 组合）→ Migration
  Proposal → 停在 Human Gate。

## 执行纪律

- 每个 WI = transient WorkItem（workitems/ 落载荷）+ fresh subagent +
  result/evidence 回 Leader。
- 失败边：实现缺陷回 IMPLEMENT；语义/假设缺陷回 Leader 修订 state.md。
- 网络下载一律经 127.0.0.1:7890；不动 .gitignore；权重不入 git。
