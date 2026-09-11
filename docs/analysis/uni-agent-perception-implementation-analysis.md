# uni-agent 感知实现现状分析（Perception Implementation Analysis）

> DocumentType: `LEGACY_PERCEPTION_IMPLEMENTATION_ANALYSIS`
>
> Status: `CANDIDATE / NOT_ADOPTED`
>
> Authority: `NONE`
>
> Date: `2026-09-09`
>
> Scope: `uni-agent` 分支感知相关实现（Python 感知服务 + C# 采集/集成 + 语义身份旁路）的功能、逻辑、架构梳理，作为 Target「造眼睛」（感知 acquisition 真实化）change 的现状证据基础
>
> 关系：与 `legacy-perception-evidence-assessment.md`（PER-002 A0/A，评估**资产**）互补——本文评估**运行时实现**；与 `uni-agent-trace-replayability-analysis.md`（Trace 侧）同体例
>
> Forbidden Boundary: 本文是 Legacy Evidence / Architecture Reference，不是 uni-harness current architecture，不构成任何协议或实现承诺。uni-agent 提供 Evidence，不提供 Authority。所有结论来自 `git ls-tree` / `git show uni-agent:<path>` 的真实代码。

---

## 0. 方法与覆盖声明

- 分支未检出，全部经 `git show uni-agent:<path>` 读取。
- **深读**（逐行/大段）：`LocalVisionPerceptionSource.cs`、`AdbScreenshotSource.cs`、`uniclaw_perception/server.py`（lifespan / `_run_pipeline` / `/v1/analyze`）、`schema.py`、`PhysicalEnvironment.cs`（ObserveAsync 全程）、`FastSemanticPipelineFactory.cs`。
- **结构读**（函数/类签名 + 关键段）：`fusion/engine.py`（`fuse_evidence` 主体）、`yolo/inference.py`、`ocr/rapid.py`、`FastSemanticContainerIdentityProvider.cs`、`SemanticEvidenceV2.cs`、`VisionServiceHost.cs`、`Observation.cs`。
- **仅盘点**（ls-tree / grep，未深读）：`operators/`（`row_relation_head.py` 42KB 等）、`row_stabilizer.py`、`evaluation/`、`preprocessing.py`/`remap.py` 细节、`ISwitchStateReader` 实现、`SemanticCapabilityRuntime` V2 admission、`VisionServiceHost` 其余部分。
- 结论以深读/结构读文件为准；仅盘点部分只描述用途定位，不下内部实现结论。

## 1. 总览

uni-agent 的感知是一条跨进程流水线：C# 负责「拿截图 + 组装观察」，独立 Python 感知服务负责「看懂截图」（YOLO 检测 + OCR + 融合），C# 把结果组装成 `Observation` 喂给 Agent；旁边一条语义身份旁路（embedding / prototype 匹配）产出 `ContainerIdentity` 证据。

```text
Agent（决策侧，ObserveAsync 触发者；Agent.cs / RunExecutionCoordinator.cs 消费 IEnvironment）
  │
  ▼
PhysicalEnvironment（IEnvironment 组合根；Adapters/PhysicalEnvironment.cs）
  ├─ ① 截屏 AdbScreenshotSource（adb exec-out screencap -p → PNG → SKBitmap）
  ├─ ② 感知 LocalVisionPerceptionSource（JPEG q92 ─UDS HTTP→ POST /v1/analyze）
  │        ┌────────────────────────────────────────┐
  │        │ Python 感知服务（独立进程，FastAPI+UDS） │
  │        │ preprocess → YOLO → OCR → fusion       │
  │        │ → remap 坐标 → 几何校验 → JSON          │
  │        └────────────────────────────────────────┘
  ├─ ③ Vision 增强：toggle 开关状态读取（帧一致性校验）
  ├─ ④ 结构化旁路：uiautomator XML（best-effort，失败不阻塞）
  ▼
Observation{Elements, ForegroundApp, SequenceNumber, StructuredElements, Sources}
  ├─▶ Agent 世界模型
  └─▶ Semantic V2 管线（Feature→Embedding/Prototype→Retrieval→Policy→ContainerIdentity 证据）
```

进程宿主：`VisionServiceHost`（`UniClaw.Vision.Host/`）管理 Python 服务生命周期（状态机 `Cold→Warming→Healthy/Unhealthy/Crashed→Shutdown`），启动时抓取并校验部署身份（`VisionDeploymentFacts`：模型/配置/pipeline 版本哈希），运行中磁盘换模型会被 `/version` 身份对照发现。

## 2. 分层架构与职责

| 层 | 载体 | 职责 | 关键事实 |
|---|---|---|---|
| 采集层 | `Runtime.Adapters/Device/AdbScreenshotSource.cs` | 新鲜设备截屏，不解释像素 | `adb -s <serial> exec-out screencap -p`；10s 超时；空输出/坏图抛异常不猜 |
| 感知服务层 | `platforms/perception/uniclaw_perception/`（Python 独立进程） | 像素→结构化候选 | FastAPI + UDS；`/v1/analyze`（JPEG）与 `/v1/analyze_raw`（RGBA）；模型单例 + 启动 warmup |
| 桥接层 | `Runtime.Adapters/Perception/LocalVisionPerceptionSource.cs` | 传输与解析，无语义 | 只管 transport mechanics；全部失败路径 fail-closed 返回空 + 诊断码 |
| 编排层 | `Runtime.Adapters/PhysicalEnvironment.cs`（`IEnvironment` 组合根） | 一次观察的时序组装 | 「Frame F → 感知 → 增强 → Observation 必须同属一次 capture」 |
| 运行时模型层 | `Runtime/Model/Observation/`（`Observation` / `ObservedElement` / `ElementBounds` / `ObservationSourceMetadata`） | 观察数据形状 | 确定性单调 `SequenceNumber`（不用墙钟）；`Sources` 双层来源元数据（primary-vision / auxiliary-structured） |
| 语义身份层 | `Semantic.Infrastructure/Fast/` + `Runtime/Capabilities/Perception/Semantic/`（V2） | ContainerIdentity 证据 | 只产证据不产事实/信念/动作；内部失败→空证据 fail-safe |

依赖方向：Adapters → Runtime Model / Capabilities；Semantic.Infrastructure 被 Runtime 消费（`ISemanticProvider`）；Python 服务零反向依赖。

## 3. 一次「看屏幕」的完整数据流

### 3.1 截屏（AdbScreenshotSource）

`adb -s <serial> exec-out screencap -p` → PNG 字节流 → `SKBitmap.Decode` → `ScreenshotCapture(bitmap, w, h)`。超时抛 `TimeoutException`；启动失败/空输出/解码失败抛 `InvalidOperationException`。无重试、无降级——失败就是失败。

### 3.2 调用感知服务（LocalVisionPerceptionSource）

- 截图编码 JPEG（quality 92），经 **Unix Domain Socket**（如 `/tmp/uniclaw-vision.sock`）`POST /v1/analyze`，默认 30s 超时。
- 三个可选请求头：
  - `X-Known-Rows`：跨帧行上下文（已知行 JSON 数组），供行稳定器复用 `row_id`；缺省 = 无状态、全部当新行；
  - `X-Capture-Stage-Views`：评测用阶段视图（raw 模型检测 / 归一化检测 / operator trace / fusion stages），**永不进入运行时候选**；
  - `X-Perception-Trace`：紧凑融合因果 trace（只含 refs + decisions，剥离重数据），**trace ≠ 决策输入**，仅诊断。
- 响应 DTO（`VisionEvidence`/`VisionCandidate`/`VisionDiagnostic`，file-scoped）：`candidates[{type,text,row_id,bounds{x1..y2}}]` + `diagnostics[{code}]` + 可选 `stageViews`/`trace`。
- 失败分类诊断码（写入当前 observation span 的 tag/event）：`OK` / `OK_EMPTY` / `INVALID_GEOMETRY` / `TIMEOUT` / `INFRASTRUCTURE_FAILURE` / `MALFORMED_RESPONSE` / `SCHEMA_FAILURE`。任何坏结果 → 返回空候选数组，**绝不让坏几何变成可点击候选**。

### 3.3 Python 端管线（server.py::_run_pipeline）

| 阶段 | 实现 | 输入→输出 | 关键语义 |
|---|---|---|---|
| preprocess | `preprocessing.preprocess` | 原图 → 裁剪(顶/底比例)+缩放图 + (scale, top_px) | YOLO/OCR 共享同一预处理像素空间；坐标最终回映原图 |
| YOLO | `yolo/inference.py::run_yolo_on_image` | PIL Image → `Detection{id,label,confidence,box,raw_label,raw_class_id}` | ultralytics YOLOv8（Android UI 检测模型 6.2MB `best.pt`）；模型按路径进程级单例缓存 + warmup |
| OCR | `ocr/rapid.py`（RapidOCR/ONNX，7.7MB en_PP-OCRv4）或 `ocr/paddle.py` | 全图或 ROI crop → `OcrToken{id,text,confidence,box}` | 两种模式：全图 OCR（默认）/ ROI-OCR（文本类检测框合并相邻后逐 crop 识别）；OCR 后端与语言启动期 fail-closed 配置 |
| fusion | `fusion/engine.py::fuse_evidence`（RapidOCR 路径）/ `fuse_evidence_from_crops`（paddle 路径） | detections + tokens → `candidates` | 见 3.4 |
| remap | `remap.remap_coords` | 预处理空间 → 原图空间 | 全部坐标在响应边界统一回映 |
| 几何校验 | `remap.enforce_geometry` / `enforce_stage_views` | evidence dict | GAP-002 响应边界强制校验：序列化的每个集合都过检，无旁路；出界 → `INVALID_GEOMETRY` |
| 产出 | — | `{candidates, yolo, ocr, diagnostics, scrollHints, metadata}` + 可选 `stageViews`/`trace` | `Server-Timing` 头带 yolo/ocr/fusion/scroll 分段耗时 |

数据形状（`schema.py`，frozen dataclass）：`Detection.to_json` / `OcrToken.to_json` 均输出**双坐标**——`bounds`（normalized 0..1，6 位小数）+ `boundsPx`（像素整数）+ `center`/`centerPx`；`Box` 自带相交/包含中心/面积等几何原语，构造期即拒绝非法值（`INVALID_GEOMETRY`）。

### 3.4 融合逻辑（fuse_evidence 主体）

1. 按 interactive labels 过滤检测（server 侧传入 `DEFAULT_INTERACTIVE_LABELS ∪ {text_block, text}`），YOLO 与 OCR 各自按 `(y1,x1,y2,x2)` 排序；
2. **文字就近归属**：对每个检测框，给全部 OCR token 打匹配分，过滤「分数>0 且垂直可归因（`_vertically_attributable`）」，按分数排序取中选 token；距离阈值 = 屏幕对角线 × `max_ocr_distance_ratio`(0.055)；
3. **行组合 operator 管线**：声明的 operator 序列执行——`uniform-list-row-grouping`（GENERATOR）→ `spacing-verifier`（VALIDATOR）；参数从根规则默认解析；`trace_sink` 可接收确定性管线 trace（离线重放支持）；
4. **跨帧行稳定器**：`stabilize=True` + `stabilize_context`（来自 `X-Known-Rows`）——stateless 设计，让同语义行跨帧保持同 `row_id`（输出侧成为 `PerceptionCandidate.RowId`，C# 侧称 `StabilizerHint`）；
5. 未匹配 OCR 的提升（`promote_unmatched_ocr`）、同线非导航去重（`dedupe_same_line_nonnav_candidates`）、列对齐 text block 提升、文本框误归属检测（`_detect_text_box_misattribution`）、行带归属/副标题/重复 section label/边缘裁剪判定等启发式（engine.py 内 20+ 函数；operators/ 目录另有 `row_relation_head.py`(42KB)、`text_relation_check`、`structured_corroboration`、`vlm_annotation` 等规则模块——本文未深读，仅定位）。

### 3.5 C# 组装（PhysicalEnvironment.ObserveAsync）

1. 截屏（span: perception.capture）；
2. 先建 frame-scoped Vision 机制（`IVisualControlStateReaderFactory`，PNG 编码同一 capture）——后续所有开关状态读数都校验**同一 PerceptionFrame 身份**，跨帧陈旧读数 fail-closed 丢弃（F4）；
3. 调感知（span: perception.vision）→ `PerceptionCandidate[]`；
4. 逐候选：adapter 边界归一化类型别名 → toggle 且 bounds 有效时读开关状态（帧一致性校验）→ `ObservedElement{text, switchState?, index, bounds, type}` + `StabilizerHint = row_id`；
5. 可选结构化旁路：`IStructuredUiHierarchySource.CaptureAsync` → `StructuredElementEvidence[]`（best-effort，失败静默为空，**永不阻塞主观察路径**）；
6. 组装 `Observation{elements, foregroundApp, seq}` + `StructuredElements` + `Sources`（primary-vision 与 auxiliary-structured 两条 `ObservationSourceMetadata`，均锚 `capture:{seq}`）；
7. 证据留痕钩子 `artifactTap`（可选；PNG + candidates + observation 整包外送，观察完成后才跑，故障完全隔离）。

### 3.6 语义身份旁路（Semantic Fast pipeline）

`FastSemanticContainerIdentityProvider`（`ISemanticProvider` 实现）：

```text
Feature 提取 → Embedding（bge-small 384 维 向量路径）或 DeterministicSemanticMatcher（V1 路径）
  → Prototype store → 向量检索（IVectorSemanticIndex）
  → IContainerIdentityCandidatePolicy（阈值/结构兼容/前身份冲突拒绝/最少证据弃权）
  → SemanticEvidence（kind=ContainerIdentity）或 ABSTAIN（空证据）
```

- 组装经 `FastSemanticPipelineFactory.CreateFromOptions`（policy 版本可配置回滚，V1/V2 均为确定性路径——**真实 embedding 模型未在此接线**）；
- 证据协议版本化封闭：`SemanticEvidenceV2`（kinds：ContainerIdentity / ElementAffordance / ContainerRelation）；
- 纪律：永不返回 Fact/Belief/CurrentContainer/Action；内部失败 → 空证据，Runtime 行为不变（fail-safe）。

## 4. 值得继承的设计纪律

1. **进程边界 = 职责边界**：C# 不解释像素，Python 不做语义决策；契约只有一张冻结的 JSON schema（evidence schema frozen 注记贯穿代码注释）。
2. **fail-closed 无处不在**：坏几何（响应边界强制校验，无旁路）、超时、坏响应、跨帧陈旧读数——一律丢弃 + 诊断码，不猜、不降级成"看起来对"的结果。
3. **双坐标纪律**：normalized + pixel 并行携带，`Box` 构造期拒绝非法值。
4. **trace ≠ 决策输入**：因果 trace 与 stageViews 是诊断/评测通道，运行时决策路径永不读（注释明确 `TRACE != CONTROL / EVIDENCE AUTHORITY`）。
5. **部署身份校验**：启动后快照模型/配置/pipeline 身份（G9-G11），`/version` 对照，运行中磁盘篡改可被发现。
6. **确定性观察序号**：`SequenceNumber` 单调递增，不依赖墙钟（裁决 6）。
7. **模型生命周期**：进程级单例 + 启动 warmup + OCR 语言解析失败即启动失败（fail-fast at startup）。

## 5. 与 Target 插口的映射（处置建议）

| uni-agent 部件 | Target 对应物 | 处置 | 依据 |
|---|---|---|---|
| Python 感知服务整包（server + YOLO + OCR + fusion + schema） | `RawArtifact` 的 producer（Capability Plane provider） | **首选复用对象**：自包含、UDS 契约稳定、schema 冻结、真机验证过（golden-run-v1 即其真实输出）；Target 侧只需「调服务 → 产出 RawArtifact」的薄 adapter，下接 PER-002 已验证的 FastPerception 链路 | §3.2/3.3；A0 §1（golden DIRECT） |
| `AdbScreenshotSource` | 感知 acquisition 的 device 截屏 | **ADAPT**：代码小、职责单一、异常面清晰，近乎直接可用 | §3.1 |
| fusion 启发式（engine.py 58KB + operators/） | RawArtifact 内部内容（provider 内部过程） | **随服务整体复用，不拆不重写**；descriptor 匹配属 provider 内部过程的语义已由 UIW-002 锁定 | §3.4 |
| `PerceptionCandidate` / `Observation` 模型 | 已被 Target `ObservationProposal`（PER-002 corpus 格式）取代 | **不迁移**：两套观察模型并存 = 第二真相 | §2；A0 §4 |
| Semantic Fast 管线（embedding/prototype/vector/policy） | UIWorld identity 策略的相似性证据来源 | **REFERENCE_ONLY / DEFER**：identity 权威在 Target 属 World Model（UWM-009）；vector/embedding 明确等 buyer（UWM deferred 4/7） | §3.6 |
| `VisionServiceHost` 生命周期/部署身份 | 无对应（Target 用 Kernel 组合面） | **REFERENCE** | §1 |
| `evaluation/` 评测框架 | 未来感知评测 | **REFERENCE** | §0 |
| `ISwitchStateReader`（toggle 状态读取） | Observation 的属性字段来源 | **ADAPT idea**（帧一致性校验纪律值得带走） | §3.5 |

**结构性发现**：uni-agent 中「眼睛」（ObserveAsync）与「手」（同文件 `ExecuteAsync`：DeviceAction → AdbOperation → ADB executor → ActionResult）绑在同一个 `PhysicalEnvironment` 组合根。迁到 Target 必须拆开：截屏归感知 acquisition（Capability Plane 观察侧 provider），dispatch 归 `IEffectDriver` 真驱动（Capability Plane 执行侧 provider）——对应 Target 两条不同协议边（P2 上游 vs P14/P15）。

## 6. 规模与质量印象

- C# 侧：文件普遍小、边界清楚、注释纪律好（裁决编号/GAP 编号贯穿）；桥接层与编排层职责分离明确。
- Python 侧：`engine.py`(58KB)/`row_relation_head.py`(42KB) 启发式密集，但公共入口（`fuse_evidence` 签名 + evidence schema）冻结稳定，operator 框架有 registry/ruleset/trace 分层；作为整体黑盒复用风险低，拆解重写风险高。
- 主要耦合点：UDS socket 约定（C#/Python 双端）、`X-Known-Rows` 行上下文协议（跨帧 row_id 语义）、模型文件路径约定——复用时三者需原样保留或显式接管。

---

## 7. 优化 / 改造建议（Proposals — 非权威，待 UniFlow change 裁决）

> 本节是建议不是决策；任何落地均需另立 change（UNDERSTAND→CLOSED），
> 并遵守 §5 处置表与 PER-002 已锁纪律（P2 ingress 唯一入口、strategy
> 确定性、missing detection 不产 observation、confidence 无 buyer 不进
> payload）。

### 7.0 改造总原则

1. **Provider 黑盒复用**：Python 感知服务整包不动（UDS + `/v1/analyze` 契约冻结），Target 侧只加薄 adapter；fusion 启发式属 provider 内部过程（UIW-002 已锁语义）。
2. **Live 与 corpus 同构**（replay parity 是最硬验收）：live adapter 产出的 observations 必须与 corpus 导入路径（`tools/legacy-perception-import/import.cs` 的 subject 命名：`ui.text.*` / `ui.detect.{id}.class` / `spatial.artifact.bounds.*` / `perception.page.signature`）语义一致——同一策略类型可同时消费真实服务输出与录制 JSON。
3. **失败也是显式语义**：继承 fail-closed 诊断码纪律；服务失败 ≠ 空屏幕观察（absence 逐边显式，协议通则 6）。
4. **无 buyer 不加字段**：confidence、provider 侧 identity、negative evidence 均不进入（PER-002 §16/§19）。

### 7.1 核心改造：三件套 adapter（建议一个 change 完成，如 PER-003「Perception Acquisition Live」）

| # | 组件 | 内容 | 处置来源 |
|---|---|---|---|
| ① | Screenshot Acquisition | `adb exec-out screencap -p` → PNG bytes → `RawArtifact.Capture(png, metadata)`；CaptureTime 由采集侧显式提供（host clock，只作 temporal provenance，ADR-0010：不是 freshness 权威） | ADAPT `AdbScreenshotSource`（§3.1） |
| ② | Live Vision Strategy | `IFastPerceptionStrategy` 实现：调用服务（JPEG q92 → UDS `/v1/analyze`），**payload = 服务响应 JSON**，解析 candidates/yolo/ocr → `ArtifactObservation`，subject 命名沿用 corpus 约定 | ADAPT `LocalVisionPerceptionSource` 的传输/解析/fail-closed 分类（§3.2） |
| ③ | Vision Service Host（组合层） | Python 服务进程拉起 / 健康 / 关闭；**不进 Kernel**，只是 Capability Plane 的 provider 进程管理 | REFERENCE `VisionServiceHost` 状态机思想，大幅简化（§1） |

**双 artifact 设计**：截屏 PNG 一个 RawArtifact（capture），`/v1/analyze` 响应 JSON 一个 RawArtifact（derived，`TransformationLineage` 记 `screenshot:art-… → vision-service:v1/analyze`）。理由：(a) 服务响应才是 strategy 的确定性输入，锚它保证 replay 稳定，规避 JPEG 编码器跨版本不确定性；(b) PNG 留作原始证据与未来 slow path（VLM）输入。

**部署身份 → provenance**：legacy 的启动身份快照纪律（模型/配置哈希，§4.5）不必照搬 Host 形态；服务响应 `metadata.{schema,pipeline,models,configHash}` 直接映射进 `TransformationLineage`/`Provenance`——模型换了，lineage 如实不同，replay 对照即可发现。

### 7.2 明确不开启 / 不迁移（防 scope creep）

| 项 | 理由 | 重开条件 |
|---|---|---|
| `X-Known-Rows` 跨帧行稳定 | row_id 跨帧连续性在 Target 属 World Model continuity 域（ADR-0014/0015）；provider 侧开启 = 第二套 identity 连续性语义；MVP 走 stateless（无 header = 全部新行，服务已支持） | 真实 continuity 场景 buyer + 与 ContinuityDemand 关系裁决（UWM deferred 27 / 协议 deferred ①） |
| Switch state 读取（`ISwitchStateReader`） | 增强项，corpus 已验证无 switch 路径；帧一致性校验纪律记录在案即可 | toggle 语义真实 buyer |
| uiautomator 结构化旁路塞进同一 adapter | Target 里它是 **P2 的另一个 producer**（结构化观察），不是 vision 的附属；塞一起会复制 legacy「双眼一源」结构 | 建议独立 change（P2 结构化 producer）；其 resource-id 可作未来 ReferentBasis 的 platform stable key 证据 |
| Semantic Fast 管线（embedding identity） | identity 权威在 World Model；UWM deferred 4/7 明确等 buyer | UIWorld identity 策略 buyer |
| `PhysicalEnvironment`「眼睛+手同根」结构 | 必须拆开：截屏归感知 acquisition（本建议），dispatch 归 `IEffectDriver` 真驱动（另一 change） | — |

### 7.3 对 legacy 服务本身的最小优化（provider 内部变更，不动契约）

1. 传输可用无损选项（`/v1/analyze` 现收 JPEG q92）：非必须——双 artifact 设计已把确定性锚在响应 JSON 上；仅当 future slow path 需要像素级一致输入时再加。
2. 采集侧失败分类（TIMEOUT/INFRASTRUCTURE_FAILURE 等）在 adapter 层转为显式 absence：服务失败 → 零 proposal（fail-closed）；`OK_EMPTY` → 零 observation（与「missing detection 不产 observation」现状一致，无需新语义）。
3. `Server-Timing`、diagnostics、`summary` 保留为 provider 可观测性，不进 ObservationProposal 载荷。

### 7.4 验收标准（change 的 ACCEPTANCE 建议）

1. 真机/模拟器截屏 → 服务 → `ObservationProposal` 全部经 `EvidenceLedger.Admit` admitted（P2 绿）。
2. **Replay parity**：同一真机帧，live 路径与 corpus 导入路径产出的 subject/value 集合语义一致（golden-run-v1 真实帧可作首例）。
3. 失败路径全覆盖：服务进程死亡 / 超时 / 坏 JSON / `INVALID_GEOMETRY` → 零 proposal + 可观察诊断，无部分产出。
4. 架构断言：adapter 不依赖 WorldModel / EvidenceLedger / identity 语义；confidence 不进 payload；同 artifact → 同 observations（确定性）。
5. 全量回归：既有 152 测试零回归（纯增量，PER-002/UIW 先例）。

### 7.5 风险与缓解

| 风险 | 缓解 |
|---|---|
| JPEG 编码跨 SkiaSharp 版本不确定 → replay 漂移 | 确定性锚响应 JSON artifact；JPEG 仅作服务传输格式 |
| Python 环境 + 双模型（YOLO 6.2MB / OCR 7.7MB）迁移部署 | provider 进程自包含（venv + 锁版本 + 模型文件 pinned）；属部署面非代码面 |
| UDS 平台限制（Windows） | transport 抽象留 localhost TCP fallback（realization） |
| adb / 设备序列管理、前台 app 假设（legacy ctor 必填 foregroundApp） | 采集侧配置化；Target 侧不假设前台 app（那是 legacy 世界真相字段，不迁移——A0 REJECT 同族） |
| 跨帧行为预期差（无 row_id 稳定后，同屏重复文本每帧新 subject） | corpus 路径现状即如此（去重靠帧内 key）；如真实场景受损，触发 7.2 的 X-Known-Rows 重开裁决 |

### 7.6 分期建议

- **P1（MVP，建议单 change）**：三件套 adapter（stateless 服务调用）+ 双 artifact + replay parity 验收。
- **P2**：结构化观察 producer（uiautomator XML 独立 P2 producer；grounding 价值高）。
- **P3（按 buyer）**：X-Known-Rows 行稳定、switch state、部署身份增强、VLM slow path（独立线，UWM §16）。
