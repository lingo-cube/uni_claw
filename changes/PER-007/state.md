# PER-007 — 感知服务选择性迁移 + 重构（管道显式化 / 可配置化）
lifecycle_state: closed · disposition: implemented · depth: decision-heavy · base: c54889e9

## Intent（WHAT/WHY）
PER-005 以「按需物化 + 黑盒复用」接入 legacy 感知服务，正确但不可持续改造：
改造产物落在 gitignored 树，无 review 痕迹，setup.sh 重跑可覆盖手工修改
（dict 修复被迫写成脚本补丁即首次摩擦）。本 change 将服务**选择性迁移**到
`platforms/perception/` 并重构：推理核心（YOLO/OCR/fusion/operators）字节级
保留；governance/evaluation/persistence 纠缠解缠；管道从 server.py 硬编码
序列升为**显式 stage + 配置驱动**——这是 OPT 系列（后端对照/模型替换/融合
调整）的地基：换配置而非改服务。

## Scope
- 迁移（选择性）：`uniclaw_perception/`（yolo/ocr/fusion/operators/
  preprocessing/remap/schema/health/config/server）+ `models/`（YOLO 6.2MB +
  OCR rec 7.7MB + 修复后 dict，**入仓**）+ `config/`（label-mapping +
  ruleset）+ `requirements/runtime.txt`。
- R1 模型解析直接化：`ocr/rapid.py::configure_ocr_models` 去
  `governance.ocr_model_manifest`，改 config 直指（ocr_rec_model / ocr_dict
  字段，包相对默认，env 可覆盖；fail-closed 保留）。
- R2 identity 瘦身：`/version` 与 `metadata.configHash` 用包内
  `compute_config_hash`/`_model_name`（V2 实证：hash 本就包内自算；V3：
  /version 自带无快照回退），去 governance.runtime_snapshot /
  config_manifest / pipeline_revision / deployment 依赖。
- R3 裁剪：cli / tools / tests / reports / training / evaluation /
  persistence.py / governance 不迁移。
- ~~R4 管道显式化 + 可配置化~~ **【2026-09-12 范围手术】剥离至 PER-008（待
  grill）**：R4 是 server.py 请求路径最大手术面，与「迁移零行为变化」混装
  会让回归归因变混；配置面设计（schema / knob 集 / impl 注册表形状）值得
  单独 grill，且 grill 时可拿 OPT 研究喂问题。
- `setup.sh` v2：venv + pip + 导入自检（物化步骤删除）；A4/A5 与
  PerceptionLiveEnvironmentTests 的 ProviderRoot 指向 `platforms/perception`。
- 根 README（provenance：upstream uni-agent SHA + delta 清单）。

## Out of Scope（禁止）
- 契约变化：路由（/v1/analyze_raw、/v1/analyze、/health、/version）、响应
  JSON schema、失败分类——全部冻结（P-1 parity 锚）。
- fusion engine（58KB）与 operators 内部逻辑零改动；YOLO/OCR 推理代码零
  改动（后端 impl 增量注册是 OPT-001）。
- 性能工程修复（并行/GC/计时补全）= OPT-001；打包现代化（pyproject）；
  **管道显式化 + 可配置化 = PER-008（已剥离，待 grill）**；
- 不改 Kernel C# 侧（除测试 ProviderRoot 指向）；不动 AGENTS.md（DIRECT
  平移 + 留痕先例：ADB-001 / PER-005 同款，产品 C# 零依赖该树）。

## Decisions
- D1 布局 `platforms/perception/` 原路径（包内相对解析零漂移）；不建
  pyproject（cwd-run 模型已验证）。
- D2 模型二进制入仓（Human 裁决，~14MB 一次性增长，脱离 uni-agent 分支
  存续）。
- D3 迁移基线 = uni-agent blob + dict 修复（94→95 行，对齐其自身 manifest
  注册；PER-005 evidence 留痕）。
- D4 回归护栏：新树对 corpus 帧响应 vs 基线 sha256 b5b7aa9b…（yolo=16 /
  ocr=11 / candidates=13）——yolo/ocr/candidates/_diagnostics/summary/
  scrollHints 语义全等；metadata.models 含绝对路径必变（断言排除，
  configHash 单独比）；推理零触碰下预期全等。
- D5 行为冻结 = 本 change 的验收核心（管道重构在 PER-008 之前，server.py
  请求路径除 R2 identity 窄缝外零改动）。

## Assumptions
- 同 venv 同权重同输入 → 推理输出确定（torch CPU / onnx CPU 确定性）；
  若全等断言失败，降级语义断言并查因留痕。
- governance 引用面 = 4 窄缝（V1 实证：server.py:81 / health.py:83,95-97 /
  ocr/rapid.py:52），解缠后无隐藏引用。

## Alternatives（被拒）
1. 原样 vendor-drop 整树（Human 明确：要复用可用部分 + 重构，非原样迁移）。
2. 继续按需物化（改造无 review 痕迹，拒）。
3. pyproject + pip -e（打包现代化非本次目标，cwd-run 已验证）。
4. 一步到位做后端 impl 注册（OPT-001 增量，本次只立注册表机制 + 现有唯一
  impl）。

## Owner-Authority impact
- 服务仍是 Capability Plane provider（非 Authority）；Kernel C# 零引用该
  树；交互只经 UDS/TCP 契约。词汇面 PER-006 词条不受影响。

## ADR refs
- 无新 ADR（布局/入仓决策记本 change；若后续 OPT 触发难逆转决策再议）。

## Residual risks
- R2 触碰 /version 与 lifespan identity 窄缝——护栏 = D4 基线对照 +
  A4/A5 + 全量回归；行为漂移即回IMPLEMENT。
- 依赖 venv 与 uni-agent 分支解耦后，后续上游修复需手工移植（一次性代价，
  README provenance 记录基线 SHA）。

## Acceptance
- A1（DETERMINISTIC）新树服务对 corpus 帧响应与基线语义全等（D4 口径；
  预期全等，降级需留痕查因）。
- A2（DETERMINISTIC）paddle 配置仍 fail-loud（A4_Paddle 原样通过）；
  OCR 模型/字典缺失时启动 fail-closed（R1 语义保留用例）。
- A3/A4（ENVIRONMENT）ProviderRoot 指向 `platforms/perception` 全绿
  （UDS /version + warmup + 非空推理 + 模拟器全链 admitted 非空）。
- A5 全量回归零破坏（Kernel + Agent）。
- A6 仓内增量 = platforms/perception（保留清单）+ setup.sh v2 + 测试指向 +
  README + state + evidence；`.perception/provider` 退役（缓存目录保留）。

## Verification
```yaml
verification:
  level: SCENARIO（A3/A4 ENVIRONMENT；A1/A2 DETERMINISTIC；A5 回归）
  method: >-
    基线对照（迁移前后同帧响应，D4 口径）+ R1 fail-closed 实证（坏模型路径
    → RuntimeError → exit 3）+ paddle fail-loud（A4_Paddle）+ ENVIRONMENT
    双例（新树 /version 冻结身份 + corpus 帧非空推理 + 模拟器全链 admitted
    非空）+ 全解决方案回归。
  expected: A1–A6 全满足
  actual: >-
    全绿：yolo/ocr/candidates/_diagnostics/summary/scrollHints/image 逐字节
    语义全等；metadata 除预期绝对路径外全等（configHash/modelId 一致）；
    env 3/3、全量 Kernel 347/347 + Agent 17/17（与 PER-005 基线一致零破坏）；
    .perception/provider 退役；R1 首版相对导入错层（ocr→config）启动期暴露
    即修（from ..config）。
  evidence: evidence/2026-09-12-per-007-provider-migration.md
```

## Status log
2026-09-12 · enter→understanding→resolving · Human 三答（选择性重构迁移 /
  模型入仓 / OPT 首批=工程+后端对照）+ 管道显式化可配置化诉求；V1–V3 实证
  （governance 引用面 4 窄缝；config_hash 包内自算；/version 带无快照回退）；
  基线快照已抓（sha256 b5b7aa9b…）。
2026-09-12 · resolving→persisted · 方案定稿（R1–R4 + 保留/裁剪清单 + D1–D5）；
  开工实施。
2026-09-12 · persisted→planned→implementing · 范围手术：R4 剥离至 PER-008
  （Human 裁决：配置化单独 grill，先把已 grill 的迁移收尾）；44 文件迁至
  platforms/perception；R1/R2 落地；setup.sh v2；测试重指。
2026-09-12 · implementing→reviewed→verifying · A1 基线对照全等（除预期绝对
  路径）；R1 fail-closed 实证；env 3/3；全量 347+17；.perception/provider
  退役。
2026-09-12 · verifying→closed · A1–A6 满足；Human 暂停令（补充设计细节后再
  续 PER-008）不影响本 change 完整性——迁移+解缠已闭环，管道配置化在
  PER-008 账上。
