# Tasks: perception-ocr-en-v4-normalization

> 覆盖 `specs/perception/ocr-backend-selection` + `specs/perception/ocr-text-normalization`
> 的实现、工程化落地与验收。

## 1. OCR 模型工件工程化(受管落地)

- [x] 1.1 将 en_v4 rec ONNX(`en_PP-OCRv4_mobile_rec_infer.onnx`)与 95 字符词典放入感知平台受管目录(如 `platforms/perception/ocr/models/`),记录 SHA-256
- [x] 1.2 新增 governance 登记文件 `platforms/perception/governance/artifacts/ocr-models/<sha256-prefix>.json`(filename/sha256/language/purpose),对齐既有 model-manifest 治理模式
- [x] 1.3 ConfigManifest `ocr` 块增加 `recModelRef` 引用受管工件;`language` 字段保留为选择键(设计 D1/D2)
- [x] 1.4 验证:未登记的权重被加载时拒绝并报错(unregistered reject)

## 2. OCR 文本归一化层

- [x] 2.1 新增 `platforms/perception/uniclaw_perception/ocr/normalize.py`,导出 `normalize_ocr_token(token) -> str`
- [x] 2.2 实现尾部标点 strip(保留语义标点如 `&`;`Developer options.` → `Developer options` 场景断言)
- [x] 2.3 实现粘连恢复(词典最长匹配 + 空格规范;`Disableadbauthorizationtimeout` → `Disable adb authorization timeout`)
- [x] 2.4 实现连字符/数字规范(0-O、I-l、` - `/`- `等变体收敛;`NAV_03- Page B` 归一)
- [x] 2.5 fail-closed:不支持/不确定的 token 原样保留,不臆造(单测断言)
- [x] 2.6 单测覆盖 spec 全部场景(`tests/test_ocr_text_normalization.py` 15/15 + governance/tests + evaluation/tests)

## 3. OCR 后端选择接入(消除配置漂移)

- [x] 3.1 `ocr/rapid.py` 读取 config `ocr.language`,映射受管 rec 工件;传 `rec_model_path`/`rec_keys_path`(RapidOCR kwargs)
- [x] 3.2 `language=zh` 保留现有 ch 模型路径(config 即回滚开关)
- [x] 3.3 不支持的值 fail-closed(初始化报错,不静默回退)
- [x] 3.4 OCR token 输出接入归一化层(full + ROI 分支统一调用)

## 4. 评估与变更治理

- [x] 4.1 评估侧新增文本级 GT/归一化断言(fixtures/reality 3 张图关键文本;可扩展断言集)
- [x] 4.2 跑 fixtures 回归(settings-root 重复计数、developer-options 开关行),确认无「修一词破一行」
- [x] 4.3 全量验证:`pytest platforms/perception` + 既有 evaluation 套件(503 passed;5 项失败全部为既有环境性,已隔离验证)
- [x] 4.4 configId/deployment 重建走既有 `build_active_identity.py` 流程(configId→`89af8c...`,deployment→`deploy:2718...`),登记变更证据(四模型 A/B 数据在 evidence/)
- [x] 4.5 收尾:`python3 scripts/finalize-change.py perception-ocr-en-v4-normalization`(tasks 勾选 + 投影再生验证)

## 5. 验收(Done)

- [x] 5.1 Validate 通过:`openspec validate perception-ocr-en-v4-normalization`
- [x] 5.2 OCR 冒烟:settings 图 `language=en` 加载 en_v4,粘连词用例全部修复,尾部标点已归一
- [x] 5.3 回滚演练:切换 `language=zh` 恢复 ch 模型,冒烟通过(空 kwargs→包默认)
- [x] 5.4 无 Runtime/融合回归:相关 capability 测试(perception-operator-rule-framework 既有测试)保持绿色

> **既有失败说明(非本 change 引入,纯净 HEAD 已复现)**:
> `test_runtime_snapshot.py::test_RSI05`(label-mapping `confidence 0.35` mutation 在
> 实际 `0.2` 值上不生效)、`test_server.py::test_default_config_loads`(0.35 vs 0.2 漂移)、
> `test_reality_repair.py` ×2(GT 记录 34 但当前模型产出 38)——均为 pre-existing drift。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `platforms/perception/`(OCR backend/normalization) | `docs/architecture/vision.md`(Vision & Semantic Perception projection;authority: NONE, canonical sources 见其头部) |
| `platforms/perception/governance/`(OCR 工件登记/ConfigManifest) | `docs/architecture/evidence.md` + 既有 perception governance 决策(`docs/decisions/perception-platform-phase4-deployment-identity-config-and-model-governance-gate.md`) |
| `platforms/perception/evaluation/`(文本级 GT 断言) | `docs/architecture/vision.md` + evaluation 决策(`docs/decisions/perception-platform-architecture-gate.md`) |
| `platforms/perception/tests/` + perception pytest | `docs/TEST_GUIDE.md` + `openspec/changes/perception-operator-rule-framework/`(既有感知测试区) |

> 注意:`docs/architecture/modules/` 在该仓库不存在;感知权威入口是
> `docs/architecture/vision.md`(CURRENT_STATE_PROJECTION,无 authority)+ 已归档/活跃
> Decision(见 authority order)。design.md「Decisions」为本次 HOW 依据。