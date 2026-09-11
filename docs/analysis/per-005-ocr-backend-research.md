# PER-005 后续 OCR 与推理后端候选调研

日期：2026-09-12。范围是 PER-005 当前 Python 感知 provider 后续的**候选实验方向**，不是对既有 `rapidocr-onnxruntime + en_PP-OCRv4`、`torch 2.2.2 + ultralytics YOLO` 部署的改动建议，更不改变 provider 黑盒、响应 JSON、双 artifact 或 Target 的 `ObservationProposal` 语义。

## 结论与决策边界

在 macOS arm64 / Python 3.11 上，一个可归因的后续实验是保持 OCR 模型与服务契约不变，把 OCR 的执行后端从 ONNX Runtime CPU 扩展为「显式选择 ONNX Runtime CoreML EP、否则 CPU 回退」的可测候选。ONNX Runtime 的 macOS Python wheel 可带 CoreML EP；EP 可按优先级分配可支持节点，未支持节点仍由 CPU EP 执行，因此“选择 CoreML”不等于整条模型都在 Apple Neural Engine 上执行。[ONNX Runtime CoreML EP 文档](https://onnxruntime.ai/docs/execution-providers/CoreML-ExecutionProvider.html) [ONNX Runtime EP 优先级与回退文档](https://onnxruntime.ai/docs/execution-providers/)

PP-OCRv5 和 YOLO 的 ONNX/CoreML 导出是独立的第二、第三类候选：前者是**替换 OCR 模型/工具链**，后者是**替换检测模型的序列化格式及执行时**。它们都不能在没有同数据、同协议基线测试之前宣称改善端到端 p50、p95、内存、功耗、召回率或融合结果。本文件未进行本机 benchmark。

## 先分清四个层次

| 层次 | 本次候选 | 不代表什么 |
|---|---|---|
| 模型 | `en_PP-OCRv4`；可评估 PP-OCRv5 检测/识别模型 | 模型升级不自动带来本机速度提升或 UI 文本召回提升 |
| OCR 库/编排 | 既有 `rapidocr-onnxruntime`；可迁移到统一 `rapidocr` | RapidOCR 不是单一模型，也不是硬件执行后端 |
| 执行后端（runtime / EP） | ONNX Runtime CPU、ONNX Runtime CoreML EP；RapidOCR 还可接 OpenVINO、Paddle、PyTorch、MNN、TensorRT | 后端可用不表示该模型全部算子被它接管 |
| 检测器部署格式 | 既有 PyTorch `best.pt`；可另行导出 ONNX 或 CoreML | 导出物不自动与现有 pre/post-process、类别映射和 NMS 输出兼容 |

这一区分直接保护 PER-005 的边界：无论 provider 内部选择何种模型、库或执行后端，只有既有响应 schema 中已被消费的候选才穿过边界；不能把 EP、provider、置信度或新模型标签变成 Target 语义。

## 候选一：RapidOCR 的统一包与可替换执行后端

RapidOCR 官方说明，历史包 `rapidocr_onnxruntime`、`rapidocr_openvino`、`rapidocr_paddle` 正逐步退役，开发转向统一的 `rapidocr`；从 `rapidocr>=2.0.6` 起，ONNX Runtime 不再是传递依赖，使用者必须自行安装选定的执行引擎。[安装说明](https://rapidai.github.io/RapidOCRDocs/main/en/install_usage/rapidocr/install/)

当前文档列出 `ONNXRUNTIME`、`OPENVINO`、`PADDLE`、`TORCH`、`MNN` 和 `TENSORRT` 等 engine type，并把 OCR 版本与模型大小配置列为独立参数；`PPOCRV4`、`PPOCRV5`、`PPOCRV6` 是模型版本选项，不是 runtime。[参数说明](https://rapidai.github.io/RapidOCRDocs/main/install_usage/rapidocr/parameters/) `rapidocr>=3.0.0` 还允许检测、方向分类、识别三阶段各自选 engine；官方仍建议先使用 ONNX Runtime CPU，且要求对应引擎库已安装。[多引擎用法](https://rapidai.github.io/RapidOCRDocs/main/install_usage/rapidocr/how_to_use_infer_engine/)

适用性判断：

- 统一 `rapidocr` 是**维护性候选**，因为旧包在退役路径；它要求先锁定实际版本、模型下载/物化方式和当前 `en_PP-OCRv4` 输出的回归基线。
- 对 Apple Silicon，TensorRT、OpenVINO 的官方能力描述不能直接成为首选理由：前者通常针对 NVIDIA 环境，后者是 Intel 技术栈；两者都需要在本机另行安装与验证。RapidOCR 列出它们是“可选引擎”，不是 macOS arm64 性能承诺。
- Paddle、Torch、MNN 也只是同一库的候选执行引擎；引入它们会增大运行时、版本和模型格式的变量，故不应与 PER-005 当前 live acquisition 验收混在同一变更中。

## 候选二：ONNX Runtime CoreML EP（优先做小范围对照）

ONNX Runtime 官方称 CoreML EP 支持 macOS 10.15+；官方 macOS Python wheel 的 `onnxruntime` 包可包含该 EP，运行时需以 `ort.get_available_providers()` 确认出现 `CoreMLExecutionProvider`。文档建议 Apple Neural Engine 设备以获得最佳表现。[CoreML EP：要求、安装和运行时确认](https://onnxruntime.ai/docs/execution-providers/CoreML-ExecutionProvider.html)

但这不是“打开开关必然加速”：EP 依据 provider 优先级接管它支持的节点，不能接管的节点可由 CPU EP 执行；而且官方指出动态 shape 会损害 CoreML EP 的性能，并提供“只允许静态输入 shape”的选项。[EP 回退机制](https://onnxruntime.ai/docs/execution-providers/) [CoreML EP 的动态 shape 说明](https://onnxruntime.ai/docs/execution-providers/CoreML-ExecutionProvider.html)

适用性：PP-OCR 的 ONNX 模型已在当前 provider 中使用，因而这是最小的实验面。应以固定尺寸或受控尺寸桶的 UI 截图分别测全图 OCR 与 ROI OCR，并记录 provider 列表、EP options、每阶段实际 session 配置及 CPU 回退。不得只凭 `CoreMLExecutionProvider` 出现在列表中声称 ANE 已参与、全图已卸载到 CoreML 或延迟已下降。

风险：算子覆盖与图分割会随 ONNX Runtime、模型和输入 shape 变化；首次 session 创建、CoreML 编译/缓存与 warmup 可能和稳态推理差异显著。必须把 cold/warm 分开，并维持与 CPU 基线同一 ONNX 文件、同一预处理、同一服务响应 schema。

## 候选三：PP-OCRv5（模型与 PaddleOCR 工具链升级）

PaddleOCR 官方将 PP-OCRv5 定义为新一代通用场景文字识别能力，覆盖简体中文、拼音、繁体中文、英语、日语以及手写、竖排和罕见字符等场景，并报告其内部复杂评测集相对 PP-OCRv4 的端到端增益。[PP-OCRv5 算法介绍](https://paddlepaddle.github.io/PaddleOCR/v3.0.0/en/version3.x/algorithm/PP-OCRv5/PP-OCRv5.html) 该结论的模型、数据集和指标口径属于 PaddleOCR 的内部评测，不能外推为 Android Settings 截屏、`en_PP-OCRv4` 到 v5 的本机或端到端提升。

PaddleOCR 3.x 的官方快速开始要求 PaddlePaddle 3.0+ 再安装 `paddleocr`；因此它不是仅替换一份 ONNX 权重的无依赖升级。[PaddleOCR 3.x 快速开始](https://paddlepaddle.github.io/PaddleOCR/main/en/quick_start.html) 官方模型表也显示 v5 有 mobile/server 变体、语言覆盖、模型大小和 CPU/GPU耗时等不同维度；这些表中数字来自其指定基准环境，不能代替本机测量。[文本识别模型表](https://paddlepaddle.github.io/PaddleOCR/main/en/version3.x/module_usage/text_recognition.html)

适用性：当 corpus 和现场帧证明英文 UI 文本存在可归因的 OCR 缺口，且该缺口不是检测、行组合或 label attribution 问题时，PP-OCRv5 才有独立 buyer。实验应先锁定“检测模型、识别模型、语言模型、PaddleOCR 版本、PaddlePaddle 版本”这一整组；不能笼统写成“升级到 PP-OCRv5”。

风险：PER-005 D7 明确跳过 Paddle，当前若通过 PaddleOCR 原生链路引入 v5，会改变环境依赖和启动失败面，需另立变更并保留 paddle 被配置时的当前诊断语义。即便选择由 RapidOCR 运行的 v5 ONNX 模型，也仍须证明模型文件、模型版本、引擎与输出 token/box 语义完全受控。

## 候选四：YOLO 的 ONNX 与 CoreML 导出

Ultralytics 官方 export 文档列出 ONNX 和 CoreML 两种格式；ONNX 输出为 `.onnx`，CoreML 输出为 `.mlpackage`，两种格式支持的参数集合并不相同，应分别核对图像尺寸、量化、动态 shape 和 NMS 的支持矩阵；导出格式被列出并不表示任意自训练 `best.pt` 都能无差异迁移。[Ultralytics Export 格式与参数](https://docs.ultralytics.com/modes/export/) 官方 CoreML 集成页说明 CoreML 推理和验证在 macOS 上运行，并给出 `model.export(format="coreml", quantize=8, imgsz=640)` 的导出范例。[Ultralytics CoreML 集成](https://docs.ultralytics.com/integrations/coreml)

适用性：若检测器阶段已被测量并确定为瓶颈，可建立单独的 `best.pt → ONNX` 或 `best.pt → CoreML` 对照。ONNX 路线可与 OCR 同样交给 ONNX Runtime（再评估 CPU/CoreML EP）；CoreML 路线可直接使用 Apple 平台执行链。这是检测器内部 realization 替换，必须维持当前类别字典、置信度阈值、坐标空间、NMS 策略和 fusion 输入结构。

风险：导出时动态 shape、NMS 内嵌与量化会改变输出形状或数值边界；量化还可能改变召回/精度。官方页面中的“最高 3x CPU”“最高 5x GPU”是其泛化导出提示，既非该 `best.pt`，也非 macOS arm64 的实测，不能用作 PER-005 性能结论。[Ultralytics Export 性能提示](https://docs.ultralytics.com/modes/export/)

## 建议的测量设计（后续独立实验）

1. **固定比较单位。** 同一已版本化截图 corpus 加同一批标准模拟器现场帧；每帧保留 OCR token 文本与框、YOLO 原始检测、最终响应 JSON，并以现有 P-1 语义集合做回归。
2. **一次只动一个变量。** 先 OCR 的 ONNX Runtime CPU vs CoreML EP（模型不变）；再 PP-OCRv4 vs 明确版本的 v5（执行后端不变）；检测器导出则独立进行。不要把模型、引擎、量化和预处理同时替换。
3. **同时量正确性与运行表现。** 记录冷启动、warmup 后单帧和批量 p50/p95、RSS、失败率，以及 token-level 文本/框差异、检测类别/框差异、最终 candidate 集合差异；端到端还要记录服务 `Server-Timing` 分段与 JSON schema/几何校验结果。
4. **记录实际而非意图。** 报告 macOS、芯片、Python、所有库、模型哈希、输入尺寸、CPU/ANE/GPU 使用配置、ONNX Runtime providers 与 EP options；CoreML EP 情形还应记录是否存在 CPU 回退。每种方案至少分别跑 cold 与 warm，并明确样本数与异常处理规则。
5. **守住 PER-005 验收。** 任意候选都必须保持坏响应/坏几何 fail-closed、`OK_EMPTY` 语义、P-1 replay parity 和 P-2 非空 proposal 环境验证；模型升级导致 JSON 中识别内容变化是预期实验结果，应用人工标注评估；若拟改变响应契约或 observation 语义，则须先回到新的架构/Change 决策。

## 推荐排序

1. 建立现有 `en_PP-OCRv4 + ONNX Runtime CPU` 与当前 `best.pt` 的真实基线；这是判断任何“提升”的前提。
2. 对 OCR 做 ONNX Runtime CoreML EP 小范围对照，检查节点回退与输出数值/类别差异。推理输出未必逐字等价，须预先定义数值容差和质量门槛；每份已捕获 JSON 的确定性解析仍须保持 parity。
3. 仅在 OCR 已被证实为质量瓶颈时，评估明确的 PP-OCRv5 模型组合；仅在 YOLO 已被证实为性能瓶颈时，评估 ONNX/CoreML 导出。

上述顺序是风险和可归因性的排序，不包含已有的本机实测提升结论。
