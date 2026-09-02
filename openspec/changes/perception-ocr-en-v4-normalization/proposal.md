# Proposal: perception-ocr-en-v4-normalization

## Why

当前感知管线声明 `ocr.language=en`,但实际加载的是 **ch_PP-OCRv4 中文 mobile 模型**(17MB,配置漂移:config `language: en` 从未生效)。25 张真实 Android 英文截图 × 4 模型(当前 ch / en_v3 / en_v4 / ch_server)的只读 A/B 证实:当前模型在英文 UI 上系统性输出**无空格粘连词**(`EnableBluetoothstacklog`、`Disableadbauthorizationtimeout`),导致下游文本匹配/查重失败——这正是「老修修补补」与 PHASE-2.6 报告 §5.1「OCR 短读 → Unknown 方差」的机制根因。换用英文专用 `en_PP-OCRv4_mobile_rec`(en_v4)可零耗时代价修复粘连,但引入尾部句号/连字符噪声,必须配套标点归一化层。

## What Changes

- **OCR rec 模型切换到英文专用 en_v4**(`en_PP-OCRv4_mobile_rec_infer.onnx` + 95 字符 en 词典),det/cls 维持现有 PP-OCRv4。
- **新增 OCR 标点/拼写归一化层**(normalizer):strip 尾部句号、规范连字符(`-`/`--`/空格 变体)、数字 `0/O`、`I/l` 混淆等,作用于 token 输出,对任意 rec 模型生效。
- **修复配置漂移**:`language` 字段真正接入 OCR 后端选择(消除 `RapidOCR()` 无参构造加载中文模型的现状)。
- **OCR 权重工程化落地**:en_v4 onnx + 词典作为**受管模型工件**纳入感知平台(不入 pip 依赖),按内容寻址(SHA-256)登记,配置引用 artifact path。
- **评估侧补充**:现有 3 张 GT 图四模型命中率打平(粒度太粗),新增**文本级 GT/归一化断言**以区分 OCR 质量(标点/粘连规则)。
- 可选(不在本次范围):det-once/rec 并发优化(5.6× 加速,另立 change);ch_server(CoreML 受 warm/动态 shape 制约,不引入)。

## Capabilities

### New Capabilities

- `perception/ocr-backend-selection`: 让 ConfigManifest `ocr.language` 等字段真正决定加载的 rec 模型(消除配置漂移),并建立「受管 OCR 模型工件 + 内容寻址引用」的引入制度。
- `perception/ocr-text-normalization`: 定义 OCR token 输出上的标点/拼写归一化契约(粘连恢复、尾部标点 strip、连字符规范),作为 fusion 消费者前的固定层。

### Modified Capabilities

- (无既有 spec 的 REQUIREMENT 变更) —— 若评审认为需表达对 `perception-actionable-toggle-evidence` 或后续 semantic perception 的影响,在 apply 阶段补充 delta;propose 阶段不上修既有 spec。

## Impact

- **代码**: `platforms/perception/uniclaw_perception/ocr/rapid.py`(rec 模型路径/词典接入)、`ocr/common.py` 或新 normalizer 模块、`config/label-mapping.json`(若需 ROI padding 不变)、`server.py`(OCR 调用链不动,仅 rec 配置变化)。
- **配置/治理**: ConfigManifest `ocr` 块(`backend`/`language`/模型 ref)、`platforms/perception/governance/artifacts/` 需登记新 OCR 模型工件(configId 变更 → deployment 重建)。
- **依赖**: 不新增 pip 依赖(RapidOCR 1.4.4 已支持 `rec_model_path`/`rec_keys_path` kwargs);新增受管模型工件文件。
- **评估**: `evaluation` 侧新增文本级 GT 断言;现有 baselines 不重写(新工件仅影响未来 EvaluationRun)。
- **运行时契约**: 不改变 YOLO det、融合逻辑、candidate/行为 schema;只改 OCR token 文本质量与归一化。
- **关联证据**: `openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/PHASE-2.6-FINAL-REPORT.md` §5.1(OCR 短读方差)、`.tmp-hf-intake/` 四模型 A/B 数据(pending 迁移为 change 内 evidence)。