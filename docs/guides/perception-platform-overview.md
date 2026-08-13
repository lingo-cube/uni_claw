# 感知平台全景阅读指南

> 一句话：这里是「感知平台」（Perception Platform）——我们自有的视觉能力栈（YOLO 检测 + OCR 识别 + 融合），以及围绕它建立的三套治理设施：评估、训练溯源、身份标识。

---

## 一、目录地图

两个根目录：

```
platforms/perception/   ← 代码 + 数据（结果都在这下面）
docs/decisions/         ← 决策与结果记录（故事线）
```

### platforms/perception/ 各子目录

| 路径 | 是什么 | 要不要读结果 |
|---|---|---|
| `uniclaw_perception/` | 生产 Python 服务本体（pipeline 代码） | 不读结果，这是代码 |
| `evaluation/` | 评估设施：`assets/`（资产清单）、`suites/`（评估套件）、`reports/`（基线/运行/预测） | ✅ 基线报告在 `reports/baselines/` |
| `training/` | 训练溯源设施：`artifacts/manifests/`（全部记录）、`artifacts/model-store/`（训练出的模型字节）、`artifacts/runs/`（ultralytics 原始输出） | ✅ 训练结果全在这 |
| `governance/` | 身份设施：`artifacts/current-active-identity.json`（身份总览） | ✅ 一个文件看懂身份 |
| `config/` | `label-mapping.json`——配置文件 | 参考 |
| `models/` | 生产模型 `android_ui_detection_yolov8/best.pt` | 参考 |
| `persistence.py` | 共享的「只写一次」JSON 工具 | 不用看 |

---

## 二、训练结果怎么读

### 先说结论

那是一次**迷你真实训练**，目的是证明「训练 → 候选 → 评估」这条链路的溯源能力，**不是训练一个好模型**：

- 模型名 `mini_synthetic_box`（测试专用，永不会进入生产）
- 6 张合成图片（4 训练 + 2 验证），1 个 epoch，CPU，耗时 48.2 秒
- 指标很差（mAP50 = 0.093）——**这是预期内的**，模型只学过 6 张黑色矩形图

### 记录都在哪

```
training/artifacts/manifests/
├── runs/             ← 3 个 JSON：RUNNING / FAILED / COMPLETED
├── candidates/       ← cand:c26b55fd….json（状态 CANDIDATE_TEST_ONLY）
├── model-artifacts/  ← 0f72dd1c….json（模型成品记录）
├── datasets/ configs/ annotations/  ← 产生模型的三类输入
└── lineage/          ← 溯源链总报告（7 个节点 + 6 条边）
```

### 逐个怎么读

- **runs/ 里的三个文件**：第一次尝试因数据集目录名写错而 FAILED——这份失败记录被**刻意保留**（诚实的历史证据），随后才有 COMPLETED。看 COMPLETED 文件里的 `trainingMetrics` 字段就是训练指标。
- **model-artifacts/**：`modelId = 0f72dd1c…`（模型字节的完整 SHA-256）。模型文件本体在 `training/artifacts/model-store/0f72dd1cb7….pt`——**用内容哈希命名，而不是 best.pt**。
- **lineage/**：回答「这个模型从哪来」的完整链条：

```
DatasetVersion → TrainingConfig → TrainingRun → Checkpoint
→ ModelArtifact → Candidate → EvaluationRun
```

  每个环节都是内容哈希引用，断掉的环节就留空（绝不编造）。

---

## 三、评估结果怎么读

```
evaluation/reports/
├── baselines/    ← 2 份不可变基线报告（JSON）
├── runs/         ← 评估运行记录
└── predictions/  ← 每次全新推理的输出
```

第一份基线对**当前生产模型**（`android_ui_detection_yolov8`）在真实截图上做了全新推理：

- settings 截图：23 个 YOLO 检测、16 个 OCR 文本、热启动一次分析约 6.2 秒
- 检测数量对照 Harness 清单的期望值：**3 个类别无一精确命中**——原因是这份历史期望没有记录「阶段语义」（原始 YOLO 阶段还是融合后阶段）。处理方式是如实标记为 `DIAGNOSTIC_ONLY / NOT_RELEASE_ELIGIBLE`，**既不改写历史期望，也不当作模型失败**。
- 证据充分性：**PARTIAL**——小语料库的真实状态，不是缺陷。

---

## 四、身份工件怎么读

`governance/artifacts/current-active-identity.json` 是全仓库最值得先看的一个文件：

```json
"active": {
  "deploymentId":      "deploy:101f5ddc…",   ← 发布单元身份（四个轴的组合）
  "configId":          "config:edb7ad54…",   ← 有效感知配置的哈希
  "pipelineRevision":  "prev:55602ff1…",     ← 行为代码 + 依赖版本 + OCR 模型的哈希
  "modelId":           "3f39b0d6…",          ← 模型字节 SHA-256
  "architecture":      "YOLOV8",
  "labelSpaceId":      "DEKI_YOLO_RAW_V1"
}
```

**核心不变式**：四个轴（模型、配置、管线、schema）任何一个变了，`deploymentId` 就变——于是任何一份预测都能回答「到底是哪套行为产生了我」。这一点是用真实测试证明过的（比如：进程启动后替换磁盘上的模型文件，进程报告的身份不会撒谎）。

---

## 五、决策文档阅读顺序

`docs/decisions/` 里的文件名自带编号，按 `*-result.md` 顺序读即可：

1. `perception-platform-phase3-graduation-result.md` —— Python 服务正式迁入平台
2. `phase4-first-evaluation-baseline-implementation-result.md` —— 第一份评估基线
3. `phase4-evaluation-foundation-graduation-result.md` —— 评估流程毕业
4. `phase4-training-dataset-reproducibility-foundation-{gate, implementation-result, graduation-result}.md` —— 训练溯源设施（gate 是设计、implementation 是结果、graduation 是验收）
5. `phase4-deployment-identity-config-and-model-governance-{…}.md` —— 身份与治理设施

---

## 六、快速上手：三个文件看懂全貌

```bash
cat governance/artifacts/current-active-identity.json          # 当前生产是什么
cat training/artifacts/manifests/lineage/*.json                # 测试模型从哪来
cat evaluation/reports/baselines/*.json | python3 -m json.tool # 基线测了什么
```
