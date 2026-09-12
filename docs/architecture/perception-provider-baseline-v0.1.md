# Perception Provider Plane Architecture v0.1

> DocumentType: `PERCEPTION_PROVIDER_BASELINE_V0_1`
>
> Status: `FROZEN / COMPONENT-BASELINE v0.1`（修订必须经 change）
>
> Authority: `COMPONENT`（组件基线：实现层权威；不修改任何产品级冻结基线，
> 与其冲突时以产品基线为准）
>
> Date: 2026-09-12 · 谱系：PER-005（live 接入）/ PER-007（迁移+解缠）/
> PER-008（管道配置化+身份+基准）→ ARCH-DOC-014 候选 → ARCH-DOC-015
> 冻结（分类学修复：已建成组件架构直接冻结，docs/README §3.3）。
> 证据见各 change state 与 evidence；不可逆决策见 ADR-0020/0021。

---

## 1. Plane 组成（L0）

```text
┌─ Target 产品侧（src/UniClaw.Kernel/Perception/，C#，零 NuGet 依赖）─────┐
│ AdbScreenshotAcquisition  设备截屏 → PNG RawArtifact（capture）          │
│ VisionServiceTransport    uds | loopback-tcp 端点（封闭层次结构）          │
│ VisionServiceClient       /v1/analyze_raw 调用 + 全失败分类              │
│ LiveVisionStrategy        响应 JSON → ArtifactObservation（parity 锚）    │
│ VisionServiceHost         provider 进程生命周期（拉起/探活/fail-loud）    │
│ PngImage                  PNG→RGBA 零依赖解码（analyze_raw 输入）         │
└───────────────────────────────────────────────────────────────────────────┘
        │ 契约：UDS（默认）/ loopback TCP · 响应 JSON schema · 失败分类
┌─ Provider 侧（platforms/perception/，Python，选择性迁移自 uni-agent）────┐
│ uniclaw_perception：server / pipeline / identity / config / ruleset 治理  │
│ 推理核心：YOLO（torch）→ RapidOCR → fusion engine + operators（黑盒保留） │
│ bench/run_l2.py：L2 录屏基准（双锚：输出哈希 + 分位守门）                 │
└───────────────────────────────────────────────────────────────────────────┘
```

分工律：C# 不解释像素，Python 不做产品语义决策；Kernel C# 零引用 provider
树，交互只经传输契约（ADR-0021）。感知 acquisition（拿像素）≠ Perception
（解释像素）——词汇面见 CONTEXT.md「Perception / Fast-Slow」（PER-004）与
PER-006（待落）。

## 2. 组件与数据流（L1）

一次 live 观察：

```text
设备 --adb screencap--> PNG bytes --PngImage--> (w,h)
  → RawArtifact.Capture(png)                     # capture artifact（内容寻址）
  → client.AnalyzeAsync(rgba) --/v1/analyze_raw--> provider
      pipeline: preprocess→detect→recognize→fuse→remap→validate→assemble
  → 响应 JSON（derived artifact，CaptureScope=derived:vision-service:art-X）
  → FastPerception("perception.live.vision", LiveVisionStrategy).Observe
  → ObservationProposal[] → EvidenceLedger.Admit（P2 唯一 ingress）
```

双 artifact 设计（ADR-0020）：capture（PNG，原始证据/slow path 输入）与
derived（响应 JSON，**确定性锚**）分离——replay/parity 锚定响应字节，
规避图像编码器跨版本漂移。

## 3. 契约（L2）

| 面 | 契约 | 纪律 |
|---|---|---|
| transport | UDS（默认）/ loopback TCP（无 host 字段=结构性执法） | 只改连接方式，不改语义（PER-005 D3） |
| 端点 | POST /v1/analyze_raw（X-Image-Width/Height 头 + w×h×4 RGBA body）；GET /version、/health | 非本机地址 = 认证/加密/治理，显式 out of scope |
| 响应 schema | yolo[]/ocr[]/candidates[]/_diagnostics/summary/scrollHints/image/metadata | **语义数组 = parity 锚**（LiveVisionStrategy 逐字段镜像 corpus 导入约定）；metadata 为 additive 可变面 |
| 失败分类 | Timeout / InfrastructureFailure / MalformedResponse / SchemaFailure / InvalidGeometry | 任何失败 → 零 proposal fail-closed；absence 逐边显式 |
| 变体选择 | 请求 header `X-Pipeline-Variant` 只可选预声明变体 | **不可携带配置内容**（ADR-0021）；未知 → 400 |
| 身份 | metadata 携带 modelId/configId/pipelineRevision/deploymentId/pipelineVariant | 四层内容寻址；变体各持身份 |

## 4. Provider 内部架构（L1 续）

- **管道**（`pipeline.py`）：STAGES 显式 DAG（串行实现；detect/recognize
  并行缝 = OPT-001）；pydantic 校验（零新依赖）；未知 stage/impl/param →
  启动 fail-closed；recognize impl 由 cfg 推导并校验（单一真相源）。
- **变体**：`config/pipeline.json`（默认=历史行为，行为冻结）+
  `config/pipeline-variants/*.json`（启动全量 lint）。
- **四层身份**（`identity.py`，uni-agent governance 平移）：
  configId（有效配置轴+管道轴，变体感知）/ pipelineRevision（行为模块源码 +
  实测依赖版本 + OCR 模型哈希）/ deploymentId（四轴 canonical hash）；
  启动期冻结快照（G9-G11：post-start 磁盘变更不渗入报告身份）。
- **ruleset 治理**：operators 注册表（GENERATOR/VALIDATOR/ADVISOR 权威分类、
  参数钉扎、lint 复杂度预算、canonical 序列化、root 默认规则）——fusion
  规则的可审计治理框架，ruleset 内容轴进 configHash。
- **基准**（`bench/run_l2.py`）：L2 录屏推理（corpus 帧 in-process），
  per-stage 分位守门（n≥10 p50/p95、n≥100 p99）+ 输出哈希（正确性锚）+
  身份引用（deploymentId）。

## 5. 纪律（L3）

1. **fail-closed 无处不在**：坏几何/超时/坏 JSON/配置不可 lint/模型缺失/
   paddle 被配置而缺位——一律显式失败，绝不静默降级（PER-005 A2 全族）。
2. **行为冻结验证**：结构变更（迁移/管道化）以「同帧响应语义全等」验收
   （PER-007/008 先例）；接线证明用 kwargs 捕获，不依赖帧内容运气。
3. **内容寻址身份**：路径无关；实测版本而非声明版本；身份轴变更 = 行为
   变更的证据责任。
4. **env = 启动期种子**：运行期动态面只有变体选择（PER-008 D5）。
5. **无 buyer 不加面**：热切换 defer（重开条件成文）、Ray/Triton/BentoML
   defer（四触发条件）、请求携带配置内容被拒。

## 6. 扩展点（已留缝，未启用）

| 扩展 | 落点 | 状态 |
|---|---|---|
| 替代推理后端（onnx-ort/coreml/mps） | detect impl 注册表（STAGE_IMPLS） | OPT-001 增量注册 |
| detect/recognize 并行 | STAGES 依赖声明 | OPT-001 |
| OCR 模型对照（PP-OCRv5） | cfg.ocr_rec_model/dict（R1 直配）+ 变体 | OPT-002 |
| 热切换（file-watch/信号/端点） | identity epoch 机制已备 | defer（重开条件见 PER-008 evidence） |
| 分布式服务化 | —— | defer（Ray 四触发条件） |

## 7. 与冻结基线的映射核对（§8 Capability Plane）

- **realizes**：§8 允许项「perception typed capability 的可替换实现」——
  本 plane 是其 live realization（IFastPerceptionStrategy 可替换缝不变）。
- **不触碰**：Owner/Authority 全景（perception 非 Authority；identity 权威
  在 World Model；P2 ingress 唯一性；confidence 无 buyer 不进 payload）。
- **依赖方向**：Kernel C# ← 契约 → provider 进程；provider 树零进入产品
  程序集（AGENTS.md 绿地隔离的 DIRECT 平移先例，provenance 见
  platforms/perception/README.md）。

## 8. 修订规则（冻结后）

本基线已 FROZEN（COMPONENT-BASELINE v0.1）：任何修订（含 OPT 系列扩展
落地后的 §6 扩展点状态更新）经独立 change 推进版本（v0.2…）；语义数组
契约（§3）变更需同时核对 ADR-0020/0021 是否需要 supersede。
