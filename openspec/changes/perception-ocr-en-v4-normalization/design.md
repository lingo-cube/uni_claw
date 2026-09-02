# Design: perception-ocr-en-v4-normalization

## Context

动机见 proposal.md「Why」:当前 OCR 是配置漂移(ch 模型加载于 `language=en` 声明下),且 25 张真实截图 × 4 模型 A/B 证明 ch_mobile 的英文粘连是「老修修补补」与 PHASE-2.6 §5.1「OCR 短读方差」的机制根因,而 en_v4 修复粘连但引入尾部标点/连字符噪声。Specs 定义了两个新 capability:`ocr-backend-selection`(配置→rec 模型 + 受管工件)与 `ocr-text-normalization`(token 归一化契约)。

相关现状(RapidOCR 1.4.4 已确认):
- `RapidOCR.__init__(config_path=None, **kwargs)`,kwargs 经 `UpdateParameters` 支持 `rec_model_path=` / `rec_keys_path=`,**推理代码零改动即可换 rec 模型**。
- 当前 `ocr/rapid.py` 的 `_get_rapid_ocr()` 无参构造 → 加载默认 ch_PP-OCRv4 全套(17MB),`language` 配置未被消费。
- 现有 ROI 分支(`server.py` ocr_mode=roi)是串行逐 crop 全 OCR 反模式(实测 7× 慢),已建议不启用;本次不改。

## Goals / Non-Goals

**Goals**
- rec 模型由 config 选择,默认落 en_v4(英文专用),消除配置漂移。
- 新增 OCR token 归一化层(粘连恢复、尾部标点 strip、连字符/数字规范、fail-closed),作为 fusion 前固定层。
- en_v4 权重(onnx + 95 字符词典)作为受管工件登记(config 引用 artifact)。
- 文本级 GT/断言进评估侧,让 OCR 质量差异可测。

**Non-Goals**
- 不做 det-once/rec 并发优化(5.6× 加速,另立 change)。
- 不引入 ch_server / CoreML(受 warm/动态 shape 制约,evidence 已记录)。
- 不改 YOLO det、fusion、candidate/行为 schema。
- 不改 OCR ROI 分支的执行策略(其反模式另议)。

## Decisions

### D1: rec 模型选择键用 `ocr.language`,经 config 注入 RapidOCR kwargs

- **决策**:`ocr/rapid.py` 读取 EffectiveConfigManifest 的 `ocr.language`,映射到受管工件(默认 `en` → en_v4 rec + 词典;`zh` → 现有 ch rec);构造时传 `rec_model_path`/`rec_keys_path`;不支持的值抛错(fail-closed,spec 要求)。
- **理由**:config 已是变更权威(label-mapping.json / ConfigManifest),语言声明应真正生效;kwargs 覆盖是 RapidOCR 1.4.4 原生能力,零新依赖。
- **备选**:升级 rapidocr(>)以支持新 provider/模型 —— 否,RapidOCR 1.4.4 已锁版本(requirements/runtime.txt),且不是必需。
- **备选**:把语言塞进 RoiPadding 之类配置 —— 否,语义错位。

### D2: 受管工件登记表放 governance artifacts,引用进 ConfigManifest

- **决策**:新增 `platforms/perception/governance/artifacts/ocr-models/<sha256-前缀>.json`,记录 `filename/sha256/language/purpose`;en_v4 onnx + 词典落盘到感知平台受管目录(如 `platforms/perception/ocr/models/`),ConfigManifest `ocr` 块增加 `recModelRef`;运行时不从 pip 目录反推权重。
- **理由**:符合 perception 平台「机器 manifests 是真相」的既有治理模式(与 model-manifests 同构);确保 unregistered → reject(spec 要求)。
- **备选**:权重放 pip site-packages —— 否,不可寻址、不可审计。

### D3: 归一化层为纯函数模块,挂在 OCR token 输出之后、fusion 之前

- **决策**:新模块(如 `ocr/normalize.py`)导出 `normalize_ocr_token(token) -> str`;在 `ocr/rapid.py` 的 normalize 输出处与 ROI 分支统一调用;规则按「粘连恢复(词典+空格规则)→ 尾部标点 strip(白名单保留 `&`/`.` 语义场景)→ 连字符/0-O 规范(保守映射)」分层;未支持的不变(fail-closed)。
- **理由**:它是纯文本变换,先于 fusion,对任意 rec 模型生效(spec 约束);独立模块易测。
- **备选**:把规则塞进 fusion/row_stabilizer —— 否,职责越界,且对非 OCR 证据不可复用。
- **备选**:只 strip 标点不做粘连恢复 —— 否,粘连才是「修补」主因,必须处理。

### D4: 粘连恢复采用「词典分词 + 保守前缀/后缀」策略,不做语言模型

- **决策**:恢复用已知短语词典(设置页常见标题/描述)做最长匹配拆分;不确定的坚决不拆(保留原样)。
- **理由**:粘连词本质是「漏空格」,最长匹配对受控词汇有效;不引入 LLM/NLP 依赖,保持确定性(fail-closed)。
- **备选**:字级语言模型(如 wordpiece)拆词 —— 否,增加不确定性与依赖,违反 fail-closed。

### D5: 评估侧新增文本级 GT + 归一化断言,不动既有 baseline

- **决策**:在 `evaluation` 侧新增断言集:对 fixtures/reality 的 3 张 GT 图,断言归一化后的关键文本(标题/开关行)与 GT 匹配,并覆盖 spec 场景样例(token 级单测);既有 EvaluationRun/baseline 不重写。
- **理由**:四模型命中率打平证明现有 GT 粒度不足(proposal Impact);文本级断言让「换 OCR 值不值」可测。
- **备选**:重跑 19 轮运行时真机 —— 否,那是运行时验收,不在本 change;本 change 只测感知 OCR 层。

## Risks / Trade-offs

- [粘连恢复可能误拆正确 token] → 限制词表 + 单测覆盖 spec 场景;fail-closed 保留不确定项。
- [en_v4 仍有连字符/句子噪声,归一化层规则欠完备] → 规则分阶段:先尾部标点 strip + 已知词典,新增规则走后续 change;文本级 GT 保持可扩展。
- [config 变更→configId 变化→deployment 重建] → 走既有 `build_active_identity.py`/config_manifest 流程,提供 migration 步骤(见下)。
- [en_v4 兼容性(输入 shape 已验证)仍可能有边缘失败] → 冒烟 A/B 已在 apply 时作为测试前置;失败则回滚 rec 选择(保留 ch 作为可选 `language=zh`)。
- [归一化层影响 row 稳定/重复计数语义] → 用 settings-root 图断言(重复计数类 GT)做回归对照,避免「修复一词,破坏一行」。

## Migration Plan

1. 落盘 en_v4 onnx + 词典到受管目录,登记 governance 工件。
2. 新增 `ocr/normalize.py` + 单测(覆盖 spec 场景)。
3. `rapid.py` 接 config→rec 选择 + 归一化调用。
4. ConfigManifest 更新 `ocr` 块(`language` 生效 + `recModelRef`);重建 configId/deployment(走既有 build 脚本);**回滚**:保留 ch 模型路径,`language=zh` 即为回滚开关,无需 git 回退。
5. 评估侧加文本级断言,跑 fixtures 回归。
6. 收尾:`scripts/finalize-change.py perception-ocr-en-v4-normalization`(tasks 勾选 + 投影再生)。

## Open Questions

- en_v4 词典是否还需补充字段(如 `\s` 空格类)以覆盖更多粘连场景 → apply 阶段按实测补充,不动 spec 契约。
- 归一化规则集初始范围(仅尾部标点+已知词典 vs 更广连字符表)→ 先保守,留扩展位;不影响本 change 验收。