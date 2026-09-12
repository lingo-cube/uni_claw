# PER-008 — 感知管道显式化 + 可配置化（+ 版本化 / 基准）
lifecycle_state: closed · disposition: implemented · depth: decision-heavy · base: 33a34d0e

## Intent（WHAT/WHY）
PER-007 迁移后的感知服务，执行管道仍是 `server.py::_run_pipeline` 硬编码
序列，fusion 调用参数散落在代码里。本 change 把管道升为显式 stage + 配置
驱动 + 四层身份模型 + 内建基准——OPT 系列（后端对照/模型替换/融合调整）
的地基：换配置而非改服务。

## Decisions（grill 两轮锤定，2026-09-12）
| # | 决策 | 来源 |
|---|---|---|
| D1 | 管道配置化：静态默认（`config/pipeline.json` = 今日行为）+ **请求级变体选择**（`X-Pipeline-Variant` header，只可选预声明、启动全量 lint 通过的变体，不携带配置内容）；**热切换 defer**（重开条件：长生命周期生产服务 + 零重启配置轮换 buyer） | Q1+Q9 |
| D2 | **四层身份模型复活**：modelId（已有）/ configId / pipelineRevision / deploymentId——平移 uni-agent governance 三纯函数 → 包内 `identity.py`；变体各持 configId；响应 metadata additive 携带 | Q2+Q11 |
| D3 | 配置层**零新依赖**：pydantic schema 校验（fastapi 已带入 venv）；热切换 defer 后 watchfiles 亦不需要；不引 dynaconf/Hydra | Q7+Q9 |
| D4 | Ray/Triton/BentoML 全 defer + 显式重开条件（多节点/多副本/集群采样） | Q8 |
| D5 | env = 启动期种子（bootstrap-only）；运行期动态面 = 变体选择 | Q10 |
| D6 | benchmark 核心件选择性回迁：L2 录屏 runner（per-stage 计时）+ PerformanceResult 统计守门（n≥10 p50/p95、n≥100 p99）+ ruleset 治理/接线/等价测试 | Q12 |
| D7 | fusion 四 knob 进 fuse stage params：`interactiveExtraLabels` / `promoteUnmatchedOcr` / `stabilize` / `maxOcrDistanceRatio`——默认 = 今日硬编码值（行为冻结） | 已采纳 |
| D8 | 结构与优化分离：stage 依赖声明为 OPT-001 并行留缝（实现先串行） | Q5 |
| D9 | metadata additive 扩展允许（P-1 parity 锚定语义数组，metadata 本就可变面） | Q6 |
| D10 | impl 注册表机制落地但枚举当下真实路径（detect=torch-yolo；recognize=rapidocr-full/rapidocr-roi/paddle-crops 由 cfg 推导并校验 ∈ 注册表，单一真相源不复制 env 语义） | 设计推论 |

## Scope
- `uniclaw_perception/pipeline.py`：pydantic schema + 加载/lint（未知
  stage/impl/param → 启动 fail-closed）+ 变体注册（`config/pipeline.json`
  + `config/pipeline-variants/*.json`）。
- `uniclaw_perception/identity.py`：configId / pipelineRevision /
  deploymentId 三纯函数平移（canonical hash 内联，不依赖 evaluation）。
- `server.py`：`_run_pipeline` 拆具名 stage（preprocess→detect→recognize→
  fuse→remap→validate→assemble，串行 + 依赖声明注释位）；fusion 四 knob
  接配置；`X-Pipeline-Variant` 选择；metadata 携带 configId/
  pipelineRevision/deploymentId/variantId；per-stage 计时归 runner。
- benchmark：`platforms/perception/bench/`（L2 录屏 runner 核心 +
  PerformanceResult 守门 + JSON 报告）+ requirements/dev.txt（pytest）。
- 测试：回迁 ruleset 治理/接线/等价 ~5 个 + 新增（变体选择/身份/lint
  fail-closed/默认行为等价）。
- README 更新（管道配置 + 变体 + 身份使用说明）。

## Out of Scope（禁止）
- 热切换（file-watch/信号/端点）；请求携带配置内容；Ray/Triton/BentoML；
  Hydra/dynaconf；并行化/GC/计时补全等性能工程（OPT-001）；新推理后端
  impl 接线（OPT-001）；fusion engine / operators 内部逻辑；契约变化
  （路由/语义数组/失败分类冻结；metadata additive 除外）。

## Alternatives（被拒）
1. 请求携带配置内容——provider 黑盒边界倒置 + 未 lint 配置进运行时 +
   身份按请求抖动。
2. 热切换现在做——零 buyer（Host 掌控进程生命周期，重启 ~2-6s；实验走
   变体选择）。
3. Ray Serve——四个触发条件零满足（多节点/多副本/集群采样/队列积压）。
4. Hydra/dynaconf——实验编排/多源分层无 buyer；pydantic 已在 venv。
5. Airflow/Dagster/Prefect/Kedro/Hamilton——批处理 DAG 框架错位同步
   请求路径；算子子管线的治理框架已存在（ruleset 机制）。
6. MLflow/DVC——注册生命周期/仓储层工具，非运行时内容寻址身份。
7. 算子代码 semver 注册表——内容哈希天然覆盖（ruleset 内容轴），无 buyer。

## Acceptance
- A1（DETERMINISTIC）默认行为基线对照：默认 pipeline.json 下同帧响应与
  PER-007 基线（`98b5c7f8…` 口径）语义全等；metadata 新增身份字段且
  configHash/modelId 不变。
- A2（DETERMINISTIC）变体选择：`X-Pipeline-Variant` 命中预声明变体 →
  行为按变体参数变化 + metadata 带该变体 configId；未命中 → 400（不静默
  回退默认）。
- A3（DETERMINISTIC）fail-closed：未知 stage/impl/param/变体名、变体
  lint 失败 → 启动或请求期失败，无部分产出。
- A4（DETERMINISTIC）paddle fail-loud 与 R1 模型缺失 fail-closed 不回归。
- A5（DETERMINISTIC）benchmark：corpus 帧跑通 L2 runner，JSON 报告含
  per-stage 计时、输出哈希、身份（deploymentId）、分位数守门。
- A6 全量回归零破坏（Kernel + Agent；C# 侧零改动）。

## Verification
```yaml
verification:
  level: DETERMINISTIC（A1–A5）+ 全量回归（A6）
  method: >-
    服务级默认基线对照（对 PER-007 基线）+ 接线证明（kwargs 捕获，不依赖
    帧内容运气）+ 变体选择/未知 400 + fail-closed 用例（7 pytest + 服务级
    R1/paddle）+ A4 env 2/2 + L2 基准双锚（输出哈希 + 分位守门）+ 全量。
  expected: A1–A6 全满足
  actual: >-
    全绿：默认 ≡ PER-007 基线（语义数组全等 + metadata 既有字段不变）；
    变体身份独立且 fuse-only 影响面（kwargs 捕获证明）；未知变体 400；
    pytest 7/7；A4 2/2；基准 default yolo p50=34.9ms 单哈希 + 变体独立
    deployId；全量 Kernel 347/347 + Agent 17/17（C# 零改动）。
  evidence: evidence/2026-09-12-per-008-pipeline-config.md
```

## Status log
2026-09-12 · enter→persisted · 自 PER-007 剥离建账（Human：配置化单独
  grill）；预置 5 个 grill 问题。
2026-09-12 · persisted→resolving→persisted · grill 两轮 + uni-agent 考古
  （四层身份模型/ruleset 机制/benchmark 库存/fusion 硬编码面/动态钩子
  清单）+ 框架全景（pydantic+watchfiles 已在 venv 的事实核verified）；
  D1–D10 锤定（Q9 热切换 defer + 请求携带配置内容被拒）；Human 确认
  落账开干。
2026-09-12 · persisted→planned→implementing · pipeline.py + identity.py +
  默认/变体配置 + server 接线（fusion 四 knob / X-Pipeline-Variant /
  metadata 身份）+ bench/run_l2.py + tests（7 用例）+ dev.txt；实现期三
  修留痕（camelCase 别名 / variantId 剥离 / bench 路径层级）。
2026-09-12 · implementing→reviewed→verifying→closed · A1–A6 全绿（见
  Verification 与 evidence）；接线证明用 kwargs 捕获替代帧内容对照（该帧
  恰无 unmatched OCR，行为对照不显差异——运气不可作验收）；defer 账
  （热切换/Ray/并行化）成文于 evidence。
