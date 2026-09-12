# PER-005 算法、库与优化候选调研

> Date: `2026-09-12`
>
> Status: `CANDIDATE / NOT_ADOPTED`
>
> Authority: `NONE`
> 仅调研，不修改产品代码、PER-005 Decisions 或架构契约；未安装候选、未运行模型 benchmark。
> 本地读取基线：uni-harness `663e19fc`；legacy 只读参考 `ab70f82d00d0ae9743ea28fc199058cc0829f549`。

## 1. 结论

有值得评估的替代方案，但没有证据证明某个新库在 UniClaw 当前 Android UI 分布与 macOS arm64 上全面更好。建议先建立端到端测量，再优先比较同权重推理后端和 OCR 模型，随后按错误分布改融合，最后才考虑整体换屏幕解析器。

PER-005 当前是服务原样接入，不是感知精度或延迟优化项目：其 Out of Scope 明确禁止 provider 内部改动，D7 固定最小环境，D9 固定双 artifact。以下涉及 provider/模型/输入格式的候选应进入后续独立 change；本报告不是重开既定决策。依据：[PER-005](../../changes/PER-005/state.md)、[PER-004](../../changes/PER-004/state.md)。

## 2. 当前实现中已核实的切入点

下表源码路径均相对于 legacy `platforms/perception/uniclaw_perception/`，通过 git blob 读取，未检出或合并 legacy。

| 事实 | 源码锚点 | 意义与候选 |
|---|---|---|
| YOLO 的 `device` 默认 `cpu`；服务调用不传设备；warmup 同样指定 CPU | `yolo/inference.py::run_yolo_on_image / warmup_yolo`；`server.py:148` | 先保持 best.pt 不变，比较 CPU、MPS、ONNX CPU、CoreML；不是先换检测器 |
| 全图模式 YOLO 完成后才跑 OCR | `server.py::_run_pipeline:147–165` | 同一只读预处理图可考虑双分支并行；ROI 模式依赖 YOLO，不能套用 |
| 每个 `/v1/analyze` 请求 finally 都 `gc.collect()` | `server.py:273–317` | 测其真实耗时及长跑内存后，再比较自动/周期回收；不能直接删除并宣称无泄漏 |
| `yolo_ms` 从 preprocess 之前开始，JSON 序列化与 finally GC 在现有计时之外 | `server.py::_run_pipeline` 与 `analyze` | 现有阶段表不能代表纯 YOLO 或完整端到端延迟 |
| 每个 detection 给全部 OCR token 计算分数并排序 | `fusion/engine.py:198–208` | 打分 O(D×T)，另有逐检测框排序成本；空间筛选可减工作量，实际收益看框数量与阶段占比 |
| match_score 同时考虑中心包含、矩形相交和中心距离 | `fusion/scoring.py::match_score` | 不能只做中心近邻筛选，否则丢失大框包含/相交关系 |
| 全图 OCR 已做一次文字检测及批量识别；服务 ROI 分支逐合并框调用完整 OCR | `ocr/rapid.py::run_rapid_ocr_on_image`；`server.py:153–162` | “启用批处理”不是全图模式的新优化；ROI 模式要测重复检测成本与漏字 |
| OCR warmup 捕获异常后返回 | `ocr/rapid.py::warmup_rapid_ocr` | `/version` 健康不证明 OCR 已成功；沿用 PER-005 A4/A5 的实际非空推理验收 |

## 3. 库与模型候选

OCR 和后端的官方资料与兼容性细节见配套 [OCR / 推理后端调研](per-005-ocr-backend-research.md)。下面的优先级是工程建议，不是实测排名。

| 层 | 优先评估 | 预期解决什么 | 必须验证什么 |
|---|---|---|---|
| OCR | RapidOCR 现有模型基线 → 新版 RapidOCR 配套 PP-OCRv5 mobile；精度不足再比 server | 中英混合、小字、复杂背景的识别质量候选 | 检测、识别、字典、归一化必须配套；新版库不能被视为现有 rapidocr_onnxruntime 的无改动替换 |
| YOLO 执行 | 同 best.pt 的 MPS / ONNX Runtime CPU / CoreML | 保留领域训练资产，降低计算与部署开销 | 算子支持、NMS、letterbox、坐标映射、阈值附近输出、冷启动与回退比例 |
| OCR 执行 | ONNX Runtime 线程预算；再评估 CoreML EP | 降低 CPU 争抢与尾延迟，尝试硬件加速 | OCR 动态宽度、图分区、CPU fallback、编译和数据搬运成本 |
| UI 检测/理解 | OmniParser 的 detector-only 对照；需要图标描述时再测试完整管线 | 补充领域外 UI、无文字图标候选 | 标签映射、Android 分布、模型开销、caption 可用性与版本身份；不能将其交互性预测直接当动作权限 |
| 高频采集 | scrcpy 持续视频流采集 | 当 ADB 单次截屏确为主要耗时时降低采集开销 | 设备部署、解码、压缩小字、旋转、断流恢复、帧序号和动作之后的新帧证明 |

**后端事实。** PyTorch 提供 macOS 的 MPS GPU 后端；这不证明旧 torch pin 与当前 best.pt 在本机完整可用或更快。ONNX Runtime 支持会话内/会话间线程配置，官方也明确并行策略可能使某些模型变慢，因此建议先测试线程数 1/2/4 与默认值，不直接叠加线程池。[PyTorch MPS](https://docs.pytorch.org/docs/main/notes/mps.html)、[ORT threading](https://onnxruntime.ai/docs/performance/tune-performance/threading.html)。

**OmniParser 版本注意。** 官方仓库将屏幕解析拆为交互区域检测和图标描述；当前 README 还列出 2026/7 的 YOLOv9-E detector，并通过模型仓库 PR 提供权重。因此实验必须固定 commit/模型 revision，不用浮动 master 自动下载；也不能笼统把所有 OmniParser 权重视为同一许可，官方对新旧 detector 和 caption 分别注明许可。本文不作许可适用性的法律结论。[官方仓库](https://github.com/microsoft/OmniParser)。

**采集注意。** scrcpy 在设备侧运行 server，将编码视频交给宿主解码；官方说明客户端和服务端协议不保证跨版本兼容。持续流适合高频观察候选，但高 FPS 不等于某个动作后获取了新证据。它替换采集机制，不替换 P2 或 freshness 权威。[官方开发文档](https://github.com/Genymobile/scrcpy/blob/master/doc/develop.md)。

## 4. 算法与工程优化顺序

### 第一组：先减少可测开销

1. **把测量补完整。** 客户端记录 capture、PNG 解码、JPEG 编码、请求全耗时、响应 artifact 和确定性解析；provider 后续记录 preprocess、YOLO、OCR det/cls/rec、fusion、remap、序列化和 GC。PER-005 内可先用外部计时及现有头观察，provider 仪表改动留后续 change。
2. **同权重后端对照。** 一次只换执行后端，维持输入分辨率、预处理、阈值和融合不变。GPU 操作完成后再计结束时间，避免只量入队。模型小、batch=1 时加速器启动/拷贝可能抵消收益。
3. **全图分支有限并行。** 在 YOLO 与 OCR 各自线程预算受控时测试并行；限定同时在途请求并保持结果稳定排序。不要直接增加 Uvicorn workers，否则会复制模型与内存；当前 async 路由中的同步推理也不因 async 自动并行。
4. **GC 对照。** Python 3.11 无参数 `gc.collect()` 执行完整回收；先测 GC 时间、RSS 与长跑趋势，再决定是否改为自动回收或受控周期回收。Python GC 无法替代原生模型内存诊断。[Python 3.11 GC](https://docs.python.org/3.11/library/gc.html)。
5. **编码消融。** 后续比较原 PNG、JPEG q92 与现有 raw endpoint 的端到端成本和 OCR 误差。RGBA 字节数为 4WH，会增大 IPC 数据量；无压缩不意味着总成本更低。改变输入格式会改变模型输入及 lineage，不能宣称与旧 JPEG 输出逐字一致。

### 第二组：融合先提速，再改变关系判断

**保持行为的候选：** 先用按 y 区间排序/网格筛选排除绝不匹配的 token，框量足够大再考虑空间索引。SciPy `cKDTree` 适合中心点近邻查询，但当前匹配还接受包含与相交，需要三类候选的并集和原 `match_score` 复核；同分排序也须保留。简单布局几十个框时，索引构建可能比扫描更贵。[SciPy cKDTree](https://docs.scipy.org/doc/scipy/reference/generated/scipy.spatial.cKDTree.html)。

**改善质量的候选：** 针对文字错绑，构造单帧 detection↔text 关系图，明确包含、相交、行带、标题/副标题角色；允许一控件多 token、未归属和歧义弃权。不要直接用“匈牙利算法”替换现有融合：标准 linear sum assignment 是一对一分配，而当前关系可一对多；需要先建模容量/角色，且会改变输出语义。SciPy 当前实现使用 modified Jonker–Volgenant 算法，并非所有 assignment 实现都等于匈牙利算法。[SciPy assignment](https://docs.scipy.org/doc/scipy/reference/generated/scipy.optimize.linear_sum_assignment.html)。

两种改动分开验收：空间筛选用冻结 detections/tokens 检查 canonical 输出等价；关系算法改良用人工关系标注检查错误归属率、未归属率和端到端目标定位，不能拿旧 JSON parity 证明精度提升。

### 第三组：按误差类型升级能力

- 漏文字/错文字优先换 OCR 模型与分辨率配置，别先换 YOLO。ROI-only OCR 的召回上限受 YOLO 文本候选召回限制。
- 漏无文字图标优先测领域 detector 与 OmniParser detector-only；识别图标含义才引入 caption/VLM，按需作为 Perception 内部补充。
- 文字都正确但控件归属错误，优先改融合标注与关系算法，大模型 OCR 通常无法直接修复。
- 采集占主要延迟且确有连续观察需求，再测 scrcpy；一次性观察保留现有截屏基线更利于比较。
- 不把图像近似相同解释为世界未变化；不复用旧 Evidence/Revision/Assurance。缓存边界与必要性另行确定，不将跨帧缓存捆绑进本优化建议。

## 5. 能证明“更好”的实验

建议起步采集 100–300 张具有代表性的截图，并把相同页面近重复帧放在同一划分，按应用/页面拆开发与留出集。覆盖英文、中文和混排、小字、深浅主题、列表/弹窗/键盘、重复标签、滚动边缘、无文字图标。该数量是探索建议，不是统计充分性保证。

| 对照 | 固定什么 | 看什么 |
|---|---|---|
| CPU / MPS / ONNX / CoreML | 相同权重、输入、阈值和融合 | 总耗时 p50/p95、冷启动、内存、原始框/类别差异、fallback；先设容差 |
| 当前 OCR / v5 mobile / v5 server | 同一原始帧与其余管线 | CER、文字检测召回、完整标签正确率、小字与中英分组、耗时 |
| 融合扫描 / 空间筛选 | 冻结 detections+tokens | canonical 输出完全一致、比较次数、fusion 耗时 |
| 旧融合 / 新关系算法 | 同样原始模型输出 | 人工标注的文字归属错误、弃权、目标定位成功率 |
| 串行 / 并行；GC；编码 | 每次只切一项 | 端到端尾延迟、长跑 RSS、错误率；模型调用数 |

每组 warmup 后重复、交错运行，记录机器/电源与热状态、模型及字典哈希、配置、依赖 lock、输入/响应 artifacts。保留每帧原始计时，样本不足时不把 p95 或小幅变化解释为稳健收益。

PER-005 P-1 证明导入与 live 解析同构，P-2 证明现场链路能产出合法非空 proposal；**两者都不证明 OCR 准确率、检测召回率或服务加速**。模型/输入/融合升级应新增质量对照，但继续保留每个响应 JSON 的确定性 replay 与失败路径验证。

## 6. 建议的决策顺序

先完成 PER-005 原样接入并拿到分段基线；随后优先做“同 best.pt 后端对照 + OCR 模型对照”两个独立实验。若 fusion 或 capture 并非耗时/误差主要来源，暂缓空间索引、scrcpy 和完整 OmniParser 替换。新方案只改变 Perception 实现和 provider provenance，不引入第二套 identity/continuity，不绕过 P2、World Model 或 Assurance。

## 7. 补充算法候选（用户追问，2026-09-12）

以下优先级取决于错误类型，不意味着应全部引入；尚未运行实验。

### 7.1 容量约束匹配：最小费用流

若主要问题是“字都识别对了，但绑定到错误控件”，可把 token→控件角色建成有容量与代价的网络：token 提供单位流，控件可接收多个 token，代价由包含、距离、对齐和角色兼容构成，另设未归属出口及惩罚。Google OR-Tools 的 SimpleMinCostFlow 支持容量与费用，适合这种可分解的分配模型。[官方文档](https://developers.google.com/optimization/flow/mincostflow)。

这是本项目应用建议，非上游 UI 效果承诺。必须先区分直接归属与父容器包含；不要用 token 全局唯一归属误删合法层级关系。若引入跨 token 的复杂组合约束，简单流模型未必能表达，需另评 CP-SAT/整数规划；不要为求全局最优而强制所有 token 归属。验收看关系误绑与弃权，不能仅看求解器成功。

### 7.2 SAHI：切片与多尺度检测

SAHI 将大图分成有重叠的切片，分别检测并映射回全图，可结合全图预测。适合评估“缩图后丢失小图标”的情况。[官方说明](https://obss.github.io/sahi/guides/sliced-inference/)、[论文](https://arxiv.org/abs/2202.06934)。

建议只对困难帧/区域启用，避免每帧多次推理；保留全图检测以覆盖跨切片的大控件，验证接缝重复框、坐标 lineage 和上下文缺失。SAHI 是目标检测机制，不会自动修好 OCR；文字识别需要单独验证裁剪上下文与行完整性。衡量小目标召回、误检与新增耗时。

### 7.3 Soft-NMS 与 Weighted Boxes Fusion

Soft-NMS 按重叠程度衰减框分数，替代 hard NMS 的直接抑制，适合“原始预测有框、后处理把真目标删掉”的对照；须获取 NMS 前输出，不能对已经删掉目标的结果做二次后处理恢复它。[原论文](https://arxiv.org/abs/1704.04503)。

WBF 按置信度合并多个预测来源的框，适合已有多模型或多尺度预测时做对照。[论文](https://arxiv.org/abs/1910.13302)、[作者实现](https://github.com/ZFTurbo/Weighted-Boxes-Fusion)。

两者不是通用 UI 融合替代：按钮框、按钮里的文字框和外层卡片可以合法重叠，不能当作同一目标平均。必须对齐类别/目标定义并区分嵌套关系；多模型分数尺度也需检查。衡量漏检、重复框以及最终定位误差，不能只观察框变少。

### 7.4 单帧结构解析与关系学习

Screen Parsing 研究直接建模截图中的元素及其层级关系，说明可以把“关系”作为学习目标而非持续堆叠局部启发式。[Apple / 作者研究页](https://machinelearning.apple.com/research/screen-parsing)。

对本项目的渐进候选是：先建立单帧空间关系图，标注同一行、标题/副标题、图标配对、父子包含；先用可解释规则或小分类器判断边，积累关系标注后再比较学习式 parser。收益目标是跨布局的组合正确率与维护成本，不预设 GNN 必然更好。图只表达当前帧候选关系，不提供跨帧 identity，也不自动新增 Target payload 字段。

### 7.5 置信度校准与选择性预测

Temperature scaling 用独立标注集拟合温度以校准分类概率，不会自动提高分类准确率，也不能从当前一个聚合 OCR score 直接恢复所需 logits。文字整串正确性、框定位正确性与类别正确性需分别定义标注目标，不能把分类校准结论直接外推为检测框可靠度。[原论文](https://proceedings.mlr.press/v70/guo17a.html)。

选择性预测用拒答覆盖率换取已接受结果较低的错误风险，可作为未来“何时追加局部识别/慢路径”的实验依据；阈值须在代表性留出集上评估 risk–coverage 与额外成本，分布漂移时不能沿用保证。[原论文](https://arxiv.org/abs/1705.08500)。

当前只适合离线评估或 provider 内部候选。没有 buyer 不把 confidence 加入 P2；弃权不表示目标不存在；任何追加感知的调度须走既定 Observation Control / Perception 边界，不形成第二套控制权。

### 7.6 主动学习：不确定性加多样性选样

从失败/不确定帧中选最值得标注的一批，再以布局/表征差异避免大量近重复截图。Core-set 研究代表性子集选择；BADGE 结合梯度表征中的不确定性与多样性，需要可访问模型及训练流程，不能直接当黑盒分数排序使用。[Core-set](https://arxiv.org/abs/1708.00489)、[BADGE](https://openreview.net/pdf?id=ryghZJBKPS)。

建议先做离线语料采样，不做在线自动训练。保留随机抽样对照以发现高置信错误；按应用/页面隔离留出集。衡量相同标注预算下的召回/关系准确率改善，不仅看选中了多少低分帧。没有标注与再训练闭环时，复杂主动学习算法没有完整收益来源。

在上述候选中，最先建议比较的是：有错绑证据时做容量约束匹配，有小目标漏检证据时做 SAHI，已有领域训练流程时做主动学习。Soft-NMS/WBF、结构学习和选择性预测分别等待后处理误删、多样布局关系错误、追加感知成本等具体证据与 buyer。
