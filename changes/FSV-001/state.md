# FSV-001 — FastScreen（ScreenParser）Integration & Replacement Validation

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: c973b3e0

## Intent（WHAT/WHY）

验证是否值得在现有 Fast Perception pipeline 中正式引入 `FastScreen`（UniClaw
产品侧组件名；首个算法候选 = docling-project/ScreenParser，YOLO11-L @1280px，
55 UI 类，apache-2.0）。两个彼此独立的实验：

- **Test A（Integration）**：YOLO/OCR 完全不变，FastScreen 作为新增 step
  （OCR 之后、runtime boundary 之前）接入，验证是否提升感知质量。
- **Test B（Replacement）**：FastScreen 承担当前 YOLO+OCR 的感知职责
  （相同 downstream contract 下 A/B），验证替代能力；真实面对 ScreenParser
  无文字输出的差异（`detector replaced, OCR retained` 是合法结论）。

产出判定建议：INTEGRATE / PARTIAL_REPLACE / REPLACE / NO_CHANGE（可组合）。
**本轮只做实验，不做正式架构迁移**（Human Gate 前停止）。

## 分类（任务书 §4）

Type: Product Architecture Validation / Exploration · Scope: Fast Perception ·
Risk: Medium · Human Gate: Required before migration。

## Current Pipeline（真实实现 trace，2026-09-12 核验）

```
[C# Capability Plane]
AdbScreenshotAcquisition (PNG→RawArtifact)
PngImage.Decode → RGBA
VisionServiceClient.AnalyzeAsync → POST /v1/analyze_raw (UDS/TCP)
   ↓
[Python provider: platforms/perception/uniclaw_perception]
server._run_pipeline (STAGES DAG, PER-008):
  preprocess   crop top/bottom 6.25%, resize ≤720w      [preprocessing.py]
  detect       run_yolo_on_image: YOLOv8 21cls android_ui_detection
               imgsz=640 conf=0.2; labels 经 normalize_yolo_label 归一
               [yolo/inference.py]
  recognize    rapidocr full-image (en PP-OCRv4 mobile rec)  [ocr/rapid.py]
               (OPT-001 S5: full-image 路径 ThreadPool-2 并行)
  fuse         fuse_evidence → candidates（operator pipeline：row grouping/
               spacing verify/chevron/search-box/toggle 启发式/晋升/去重/
               stabilize/publication partition）            [fusion/engine.py]
  remap        坐标回原屏空间；enforce_geometry (GAP-002)
  → evidence JSON {yolo[], ocr[], candidates[], summary, metadata, scrollHints}
    schema = uniclaw.localVisionEvidence.v1
   ↓
[C# Runtime Boundary / Current Processing]
VisionServiceClient envelope 校验（yolo/ocr 数组必须在；INVALID_GEOMETRY fail-closed）
ResponseJson → RawArtifact (D9 derived artifact, 确定性锚)
LiveVisionStrategy.Observe → ArtifactObservation：
  yolo[] → ui.detect.{id}.class + spatial.artifact.bounds.detect.{id}
  ocr[]  → ui.text.ocr{i}    + spatial.artifact.bounds.ocr{i}
FastPerception.Observe → ObservationProposal[]（provenance/context）
   ↓
[C# Runtime]
EvidenceLedger.Admit → WorldModel.Reconcile（IUiObservationStrategy →
ProposedOccurrence → Occurrences/Containers/LogicalItems）
→ ResolveCurrent (P23 grounding) → Control → CanonicalBinding → Effect
```

关键事实：

- **Runtime 真正消费的对象** = 响应中 `yolo[]`（id/label/boundsPx）与
  `ocr[]`（text/boundsPx）两数组（LiveVisionStrategy 是唯一解析者；
  `candidates` 数组在响应内但当前无 C# production buyer——测试组合
  LiveFrameOccurrenceStrategy 只 join detect class+bounds）。
- confidence 不进 C# observation payload（§19 无 buyer）；missing detection
  不产 observation（§18）。
- 管道变体机制（PER-008）：`X-Pipeline-Variant` 头选预声明变体；
  STAGES/impl 注册表在 pipeline.py（detect: torch-yolo/torch-mps）。
- 基线冒烟（本机 CPU，settings-home 1080×2400）：yolo 471ms / ocr(并行)
  0ms / fusion 18ms；26 yolo / 16 ocr / 9 candidates。
- 评测基建：内容寻址资产 + gt-{assetId}-v1.json（历史 GT 多为 counts/texts
  级，elements 级空）+ bench/run_l2.py（分位计时+输出哈希双锚）+
  bench/score.py（matcher-greedy-v1：class 兼容 + IoU≥0.5 贪心一对一，
  P/R/F1）。现有 GT 资产仅 ~7 帧——不足以支撑本轮验证集。

## Scope

1. **Work 1 — Explore & Contract**（Leader direct，已完成）：pipeline trace、
   接入点/协议确定、验证集定义、Acceptance/Verify 定义（本文件 + plan）。
2. **Work 2 — Integration Test**：FastScreen provider + adapter + 管道变体
   `fastscreen-integration`；业务验证集采集 + GT；A/B 指标采集。
3. **Work 3 — Replacement Test**：detect impl `screenparser` + 变体
   `fastscreen-replacement`（B1：detector 换、OCR 留）+ B2 消融
   （OCR 关闭，诚实记录文字能力损失）；同验证集同 scorer A/B。
4. Leader Review/Verify + 结果判定 + Migration Proposal（Human Gate 报告）。

## Out of Scope（本轮禁止）

- 删除/升级现有 YOLO 或 OCR；修改 Runtime contract；修改 LiveVisionStrategy
  语义；正式架构迁移；引入 ScreenVLM production dependency（仅允许后续
  exploratory probe，且不在本轮）。
- 默认管道行为变化（default pipeline 输出必须逐字节不变——回归锚）。

## Decisions

- **D1 候选模型与 provenance**：docling-project/ScreenParser `main`
  （ScreenParse v2, YOLO11-L, 55 类, imgsz=1280, 推荐 conf=0.10 iou=0.10；
  训练分布 = web 截图——对 Android 原生 UI 属 OOD，必须如实测量）。
  权重 `platforms/perception/models/yolo/screenparser_v2/best.pt`
  （153,259,543 B，sha256
  `dbcb4f583ccfdb8100a68e606525c247890a2de4c1a54b14741e0ee29ce0ab88`，
  经 127.0.0.1:7890 代理下载；**不入 git**——正式 vendor 决策留给 Human
  Gate；venv ultralytics 8.4.115 已验证可加载，55 类与 model card 一致）。
  本地 `.tmp-hf-intake/macpaw_yolov11l_ui_detection.pt` 经查 **不是**
  ScreenParser（5 类 macOS AX 模型），不可混用。
- **D2 FastScreen 接入点（Test A）**：Python provider 内新增 opt-in stage
  `screenparse`，位置 = detect∥recognize 完成之后、fuse 之前（ honoring
  任务书图示顺序 YOLO→OCR→FastScreen→Current Processing→Boundary；fusion
  代码与参数零修改，只是消费合并后的 detection 池）。输入 = proc_img +
  yolo detections + ocr_tokens；输出 = (a) 合并 detection 池——漏检救援
  元素（确定性规则：canonical 映射后与全部现有 detection IoU<0.3 且
  conf≥阈值 → 追加），参与 fusion 全流程（text association/row grouping）；
  (b) additive `screenParse[]` 原始适配证据（含 structural Optional
  Evidence + corroboration 诊断：与 YOLO 的高 IoU 标签分歧记录，默认不改
  写既有标签——保守 v1）；(c) Server-Timing 增设 screenparse 分段。
  默认管道（无变体头）逐字节不变。
- **D3 替换点（Test B）**：detect impl 注册表新增 `screenparser`
  （FastScreen provider 产出 Detection[]，label 经 55→canonical 映射），
  变体 `fastscreen-replacement`（B1）。B2 = bench 层消融
  （ocr_tokens=[] → 空 ocr[]），不改服务配置面——诚实记录
  “文字能力归零”的实际后果，不伪造替代完备性。
- **D4 Adapter 原则**：Third-party 适配 UniClaw——ScreenParser 原生
  bbox/class/confidence → FastScreen Provider → Adapter → 现有 canonical
  perception 标签词汇（button/list_item/input/switch/checkbox/tab/toolbar/
  icon/image/text_block/popup/slider/scrollbar…）。未映射类（List/Window/
  Calendar 等结构类）保留在 `screenParse[]` 作 Optional Evidence，**不**
  为第三方模型重设计 Runtime ontology。映射表 = 交付物，Leader review。
- **D5 验证集**：`platforms/perception/evaluation/validation/fastscreen-v1/`
  ——自注册模拟器 p26_pixel（1080×2400, emulator-5554）真实采集
  30–50 screenshots + 5–10 interaction sequences，覆盖任务书 §14 分层
  （list/sidebar/settings/dialog/scrollable/dense/text-heavy/icon-heavy +
  scroll/click before-after + page transition）。来源 = 当前业务 UI
  （Android Settings 系及预装应用），不用 ScreenParse 官方 web benchmark。
- **D6 GT 方法**：优先 human-visible 标注——Leader 亲手全框标注 **核心
  calibration 子集（≥5 屏）**；其余帧以 uiautomator dump（a11y tree）
  作候选生成器 + 可见 UI overlay 人工核对修正；a11y 只作辅助验证不作
  GT 默认真值（任务书 §15）。GT 含：element 级（gtClass + normalized
  bounds + text? + interactive?）+ expectedTexts + grounding tasks。
  GT provenance（方法/已知偏差）随集记录。
- **D7 指标**（四臂同 scorer：baseline / A / B1 / B2）：
  Element Recall/Precision（matcher-greedy-v1 语义：class 兼容 + IoU≥0.5）；
  BBox IoU 分布 / center 偏差；Primitive/UI Type Accuracy；Text Exact
  Accuracy + Character Error Rate；Actionable Grounding Accuracy
  （目标解析 → normalized locator → GT hit-test，查询族 = 点击指定文本 /
  第 N 个 item / 当前选中态）；Latency P50/P95（Server-Timing 分段 + wall）
  ；Memory（推理期 RSS 采样）。所有臂同 device（主表 CPU=生产默认，
  MPS 作辅助表）。
- **D8 变量隔离**：Test A 唯一新增变量 = FastScreen step（变体开关）；
  Test B 唯一变化 = detect impl（B1）/ +OCR 关闭（B2）。同截图、同预处理、
  同 fusion、同 scorer、同 GT、同 device。默认管道回归锚 = 既有资产
  响应逐字节不变。

## Assumptions

- ScreenParser 推理（YOLO11-L @1280）在本机可承受（CPU 慢——正是要测的
  latency 维度；MPS 辅助测量）。若 CPU 不可用性导致实验失败，如实记录，
  不静默换设备粉饰。
- 模拟器 p26_pixel 可 boot（本轮已实测在线）且 Settings 树可提供 §14
  全部场景；若个别场景缺失（如 dark mode），记录覆盖缺口，不伪造。

## Alternatives（含被拒）

- C# 侧接入 FastScreen（新 strategy）——拒：模型是 Python 生态，C# 侧需
  第二服务/协议，违反最小侵入与现有 seam 结构。
- pre-fuse 作为 fusion 第五输入——拒（Test A）：改变 fusion 输入面 =
  改 Current Processing 语义，超出“新增 step”边界；但 Test B 天然经
  detect impl 走 fusion（合法：那是替换而非叠加）。
- ScreenVLM 混入主实验——拒（任务书 §18）：变量污染。
- GT 全量人工标注 30–50 屏——拒（本轮成本）：calibration 子集 + a11y
  辅助 + 人工核对已满足 §15 的诚实性要求，偏差被记录。

## Owner-Authority impact

无变化。FastScreen = Perception/Capability Plane 内 provider-side 组件，
产 Evidence/Observation，不触碰 EvidenceLedger admission、WorldModel
belief、ContainerIdentity、binding authority。Runtime contract 零修改。

## Acceptance（= 任务书 Definition of Done）

1. ✅ 当前 Fast Perception pipeline 已准确理解（本文件 trace，源自真实
   symbol/call path）。
2. FastScreen 接入协议符合现有 pipeline context（D2/D3/D4；adapter 交付）。
3. Integration Test 在真实 pipeline 上完成（默认管道回归锚通过 + A 臂指标）。
4. Replacement Test 在相同 downstream contract 下完成（B1/B2 臂指标）。
5. 两实验变量互相隔离（D8；隔离性有回归证据）。
6. 使用 UniClaw 自身业务测试集（D5）。
7. Accuracy / Grounding / Latency / Resource 均有证据（D7；evidence/ 落档）。
8. 明确判定建议：INTEGRATE / PARTIAL_REPLACE / REPLACE / NO_CHANGE
   （含组合），附 Migration Proposal。
9. 正式迁移未执行（无 YOLO/OCR 删除、无 Runtime contract 修改、
   ScreenParser 权重未 vendor 入 git、默认管道行为不变）。

## Constraints

- 网络：GitHub/HF 下载经 127.0.0.1:7890 临时代理；网络配置不成本任务内容。
- ENVIRONMENT 级验收遵循 docs/agents/test-emulator.md 注册事实。
- 产品代码不渗透 Harness 层；provider 改动限 platforms/perception 树
  （+ 必要的 C# 测试侧验证，不改生产 C# 语义）。

## Verification（声明格式：method · expected · actual · evidence ref）

- V1（CONTRACT）：默认管道回归——既有 GT 资产（golden-run-v1 3 帧 +
  settings-home + wifi 2 帧）无变体请求响应与 baseline 逐字节一致；
  expected=byte-equal；evidence=evidence/2026-09-12-fsv-001-*.md。
- V2（DETERMINISTIC）：provider pytest——screenparse 模块单测（适配映射、
  漏检救援规则确定性：同输入同输出）；expected=全绿。
- V3（SCENARIO）：A/B/B1/B2 四臂 × 验证集全帧 scorecard + 指标表 +
  latency P50/P95 + RSS；expected=每臂每指标有值或显式 NOT_SCORABLE。
- V4（CONTRACT）：Runtime 兼容——C# VisionServiceClient envelope 校验
  + LiveVisionStrategy 解析 A/B 臂响应成功且零 Runtime 代码修改；
  ground 走 LiveFrameOccurrenceStrategy 同构 join。
  **已证（2026-09-12）**：FastScreenArmContractTests 11/11 绿（四臂 ×
  数量/subject/bounds 契约 + 确定性 + additive screenParse 被忽略 +
  rescued fs_ 经常规 ui.detect.* 进入 + B2 空 ocr 零文字观察 +
  FastPerception 四臂 proposal 组装）；fixtures = 真实四臂响应
  （tests/.../Corpus/FSV001Arms/，valset 帧 058f62426c1f）。
- V5（ENVIRONMENT）：模拟器交互序列采集 + grounding 任务真实性。
- 完成判定 = Acceptance 1–9 全部有证据 + V1–V5 通过（或如实记录失败）。

## Status log

- 2026-09-12 · UNDERSTAND→RESOLVED→PERSISTED · Leader 完成 pipeline trace
  （真实 symbol）、ScreenParser 本地验证（55 类加载 OK）、权重下载、
  基线冒烟、模拟器 boot；D1–D8 决策定型。
- 2026-09-12 · PERSISTED→IMPLEMENT · WI-1 验证集（39 帧 × 8 分层 +
  8 序列，validate/replay OK）、WI-2 integration 变体（pytest 44 绿 +
  默认字节回归锚）、WI-3 replacement + B2 消融（pytest 58 绿，B1/B2
  live 实测）先后落地并经 Leader 抽验。WI-4 GT 候选生成、WI-5a 评测
  harness 进行中。
- 追加决策 D9：验证集暂不资产化（不写 assets/manifests，Leader 裁决
  C）——资产化属正式固化动作，留 Human Gate 后按需执行（Migration
  Proposal 记录帧级/集级两条备选路径）。
- 修订 D6 → D6r（RESOLVE 失败边，留痕）：本会话 Leader 模型无图像输入、
  路由无视觉 tier，"Leader 亲手视觉校正"不可执行。替代 = 确定性校正
  （calibrate.py：R1 QS 合并串剥离 ×10、R2 icon/image/slider 的
  content-desc 文本置 null ×14——规则源自 a11y 元数据语义，非模型输出）
  + baseline 交叉旗标（OCR 文本重叠/YOLO-GT 覆盖，仅旗标与排除、绝不
  书写 GT）。排除 3 个强 stale-dump 帧（5df0424f/9f5d4e04/d8b2a487，
  ocr_hit 0.00–0.10）；dialog 帧低 OCR 命中按设计豁免（GT 不含被遮挡
  背景）；count-ratio partial 排除撤销（YOLO 文字 extent vs GT 整行
  extent 的框语义差异是混杂因子，几何样例已证实：y 对齐、宽度异）。
  GT provenance 如实标注 wi4-agent + leader-deterministic-calibration；
  此方法学局限进入 Human Gate 报告披露。
- 追加决策 D10：MPS 辅助表取消——本机 MPS 实测比 CPU 慢（WI-3：
  2233ms vs 626ms）且四臂无全 MPS 变体组合（integration 无 mps 变体），
  设备公平性无法成立；CPU 主表 = 生产默认 device（torch-yolo cpu），
  如实记录。
- 2026-09-12 · IMPLEMENT→REVIEW→VERIFY · WI-4（GT：agent 候选 + D6r
  确定性校正，3 帧排除）、WI-5a/5b（四臂 harness + 全量跑，pytest 115）
  落地；V4 契约测试 11/11（Leader 直接）；V5 live 闭环 1/1；C# 全量
  361+17 绿；四臂 scorecard 落 reports/fsv001。判定建议 NO_CHANGE，
  Human Gate 报告 = evidence/2026-09-12-fsv-001-validation.md。
  停在 Human Gate（正式迁移未执行，Acceptance 1–9 全有证据）。
- 2026-09-12 · VERIFY→RESOLVE（语义缺陷失败边）· 官方文档核查（WI-5a
  迟到产出）发现输入域混杂：screenparse stage 消费 preprocess 后图像
  （crop 6.25%×2 + resize≤720w），官方 operating point = 整屏截图
  （model card 用法原文）；实测整屏输入每帧多检出 ~25% 元素
  （devopts-top 37 vs 28 / settings-home 38 vs 30）。对 ScreenParser
  系臂（A/B1/B2）系统性不利，verdict 证据被污染。
  修订 D11：screenparse stage 改摄入原图 + 坐标逆映射回 proc 空间
  （fusion/remap 语义不变）；rescue 阈值维持 0.35（98/117 text_block
  碎片@均值0.53 的解剖证据反对降低阈值）；structural 通道经查 v2 为
  leaf 注释训练（39 帧容器类检出=0），Optional Evidence 按设计即为空，
  报告披露。四臂重跑后更新 Human Gate 报告。ScreenVLM = 层级/结构
  能力的正确归属（M4 候选确认），本轮仍不混入。
- 追加记录（M4 素材，不执行）：uni-agent 分支考古（WI-5a 迟到产出）
  ——Slow 线两批可复用基准资产：(a) 71 查询 grounding 基准
  （VLM-COMPARE-QWEN3B-VS-UITARS.md；Qwen3B 定位中位 6px，密集小字页
  136px+ 系统性难点；30% fail-closed 拒答）+ 71 元素类型判别（Qwen 82%
  vs UI-TARS 27%；'Color' 幻影行同向错误=与我们 K/F 类根因同构）；
  (b) 9 核心题 dump 判型基准（YOLO+OCR dump 序列化 + 文本 LLM 8/9
  > 本地 3B 7/9 > 截图-VLM ≤4/9；判别信号已存在于感知原始输出）。
  资产位置：openspec/changes/runtime-iterative-full-traversal-acceptance/
  evidence/ 与 docs/analysis/slow-semantic-text-dump-proposal.md（git
  ref uni-agent:）。对 FSV-001 的启示：标签-vs-行判别（F/G/K 根因）
  的既有证据指向"感知 dump + 文本 LLM"而非截图-VLM——M4 开题时的
  候选优先序输入。本轮不移植（§18 变量隔离）。
- 2026-09-12 · WI-6 完成（输入域修正 + v2 重跑）· pytest 136 绿；
  baseline 锚 Δ=0；v2 关键修订：B1 元素 R 0.180 反超 baseline 0.161、
  F1 打平、typeAcc 0.750 / bboxIoU 0.792 / center 0.435 更优；
  grounding 平价 0.370（v1 的 0.435 为 resolver 换框伪优势）；
  icon-heavy 唯一弱层（R 0.090、grounding 0）。判定修订：NO_CHANGE
  维持但依据改为「代价/收益不成比例」；PARTIAL_REPLACE 升级为
  「延迟预算放宽条件下的候选项」。Human Gate 报告已重写为 v2 主数据
  （v1 标注为混杂中间轮保留 provenance）。
- 2026-09-12 · M4 后置探针（ScreenVLM）落档 · 任务书 §18 允许的
  exploratory probe（主实验收口后执行，不参与 A/B 变量）。结果：web
  分布内正常；Android 原生崩坏级 OOD（幻觉网站导航 / 206 元素全塌缩
  "text" / grounding 8=71 的 11% / 9 核心题全错）。结论：ScreenVLM 作
  Slow 层限 web 域，Android 需域微调；佐证 dump+文本 LLM 判型路线。
  产物：reports/fsv001/screenvlm-probe/（含 uni-agent 基准语料导出 +
  PROVENANCE.md）。环境事件披露：探针曾污染 perception venv 破坏
  YOLO 字节锚（3 项回归失败），已还原；Leader 独立复核 pin + pytest
  136 复绿。M4 开题素材完备（71-query grounding + 类型判别 + 9 核心
  题 + ScreenVLM 域差距实证）。

- 2026-09-12 · VERIFY→CLOSED · **Human Gate 裁决落地**：人类裁决 = 采纳
  Leader 建议——M1 保持现状（FastScreen 系保持 opt-in 实验变体，默认
  管道零影响；权重不入 git 维持 D1）；M2 资产化暂缓；M3 挂起（重启
  条件：延迟预算 ≥900ms/帧 且 mobile 域微调 checkpoint 免费出现）；
  后续方向 = 讨论 Slow 层做法（M4 线，第一步候选 = 感知 dump + 文本
  LLM 判型，素材已备）。Acceptance 1–9 全证据闭环；无未授权改动；
  文档同步；无阻塞 Human Decision。变更关闭。
- 2026-09-12 · CLOSED 后追加（人类裁决：清 HF 缓存 + 选择性保存）·
  HF 缓存 ScreenVLM（~1GB）已清（复现配方在 screenvlm-probe/
  PROVENANCE.md）；验证集精选 10 帧（8 分层 + 暗色×3 + 序列×4）按
  CALIBRATION+REGRESSION 登记入 assets/manifests（D6r GT 方法随
  manifest 携带；其余帧留 validation/ 未登记）。
