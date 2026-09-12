# Perception Evaluation Assets（错题集 · CORPUS-002 迁移）

> 体系照搬 uni-agent `evaluation/asset.py`（Phase 4 gate 冻结设计）：
> **内容寻址身份**（assetId = sha256(bytes)，路径/文件名永不入身份）+
> **正交九维分类**（Provenance × CorpusRole × SystemFamily × ScenarioDomain
> × PerceptionTask × ComponentClass × Difficulty × Criticality × Admission）
> + groundtruth 分离（gt-{assetId 前缀}-v1.json）+ suite 版本化（成员变更
> → 新 content-addressed suiteId）。

## 目录

```text
assets/
├── captures/                    真实采集字节（RECORDED_REALITY）
│   ├── golden-run-v1/           3 帧：case-a-before / case-b-off / case-b-on
│   │                            （开关翻转对！RUN-002 语义的确定性资产）
│   │                            + perception/ 服务响应 JSON ×3 + trace ×2
│   │                            + device-profile + scenario-manifest
│   ├── wifi-slice2-calibration/ Wi-Fi 开/关 2 帧 + 服务响应 + provenance
│   ├── settings-home-api35-full.png  （哈希 b057d131…，与 uni-agent 既有
│   │                                  manifest 吻合——找回的缺字节资产）
│   ├── settings-real.android-ui-yolo.evidence.json        ┐
│   ├── vision_test_controlled_screen.evidence.json        │ INFORMATIONAL
│   └── vision_test_controlled_screen.android-ui-yolo…json ┘ 证据 JSON
├── fixtures/                    合成字节（SYNTHETIC）
│   ├── synthetic-1.png          （哈希 2125e6f8…，ADMITTED + gt）
│   └── synthetic-2.png          （哈希 4c257d14…，ADMITTED + gt）
├── manifests/                   内容寻址资产登记（{assetId}.json）
└── groundtruth/                 答案集（gt-{assetId 前缀}-v1.json ×3）
```

## 资产清单（10 manifest）

| assetId 前缀 | 字节 | roles | admission | groundtruth |
|---|---|---|---|---|
| ea687421（golden-a-before）| ✅ | GOLDEN+REGRESSION | ADMITTED | golden-run manifest |
| 25aa6515（golden-b-off）| ✅ | GOLDEN+REGRESSION | ADMITTED | golden-run manifest |
| a2242734（golden-b-on）| ✅ | GOLDEN+REGRESSION | ADMITTED | golden-run manifest |
| b057d131（settings-home）| ✅（找回）| CALIBRATION | ADMITTED | gt-b057d131 ✅ |
| 2125e6f8（synthetic-1）| ✅ | CALIBRATION | ADMITTED | gt-2125e6f8 ✅ |
| 4c257d14（synthetic-2）| ✅ | CALIBRATION | ADMITTED | gt-4c257d14 ✅ |
| d3bf7e74（wifi-slice2-off）| ✅ | CALIBRATION+REGRESSION | ADMITTED | 服务响应 |
| 76a918ef（wifi-slice2-on）| ✅ | CALIBRATION+REGRESSION | ADMITTED | 服务响应 |
| 0ef2b572（settings-diag）| ❌ 字节丢失 | CALIBRATION | NEEDS_GROUND_TRUTH | 无 |
| 00191e43 / 06beaa36 / 2a9879c7 | ✅（证据 JSON）| 无角色 | INFORMATIONAL_ONLY | N/A |

## 使用（OPT-001 验收入口）

```bash
# 全部 ADMITTED + REGRESSION/GOLDEN 角色资产过 L2 推理
cd platforms/perception
../../.perception/venv/bin/python bench/run_l2.py \
  --image evaluation/assets/captures/golden-run-v1/frames/case-b-off.png --runs 10

# 后端对照的回归保护：换后端后逐帧跑 REGRESSION 角色资产，输出哈希对拍
```

## 已知限制

- `0ef2b572`（settings-diag-20260803.png）字节不在 uni-agent git——manifest
  保留（NEEDS_GROUND_TRUTH + CALIBRATION 分类有价值），标注缺字节。
- Target 测试侧 corpus（`tests/.../Corpus/`）与本病区**互补不重复**：corpus
  承载 PER-002 语义验收帧；evaluation/ 承载 provider 质量评测资产。golden
  case-a-before 字节在两处各一份（不同角色消费：corpus 做 parity 锚、
  evaluation 做 GOLDEN 角色资产），assetId 一致（内容寻址天然去重语义）。

## 基准报告（CORPUS-003 迁移，历史基线）

`reports/`：12 份 uni-agent 基准（内容寻址命名）——

| 目录 | 内容 |
|---|---|
| `baselines/` ×2 | 质量基线（qualityScorecard / safetyScorecard / performance / coverage / holdoutStatus / numericThresholds）|
| `predictions/` ×7 | 两次评测 run 对 4 资产的逐帧预测报告（含 per-stage timings）|
| `runs/` ×3 | 评测 run 元记录（suiteId / terminalStatus / environment）|

**可比性判读**：
- ✅ **质量维度可比**：`deployment.model_id = 3f39b0d6…` 与当前部署一致
  （同一 YOLO 权重）；OCR 同 rapidocr——同模型同资产的质量对拍有效。
- ❌ **性能维度不可比**：环境 Darwin **x86_64**（当前 arm64）；且身份字段
  `pipeline_revision: "1.0.0"` / `config_identity: LEGACY_PARTIAL…` 是
  PER-008 四层身份之前的旧体系——性能基准以本机 `bench/run_l2.py` 重采
  为准（旧数据仅作架构对照参考）。

`bench/benchmark_raw.py`：legacy HTTP 基准脚本（raw vs JPEG 路径对照），
依赖 `requests`（`pip install requests`），作参考工具迁入。

## 历史基线的正确用法（OPT-001）

换后端/改融合后，对 4 个有 gt 的资产重跑评测 → 新 scorecard 与
`baselines/` 对照：**质量不降级 + bench 性能提升** = 双过关。

## FSV-001 验证集精选登记（2026-09-12，选择性保存）

自 `validation/fastscreen-v1/`（39 帧，uniclaw.fastscreenValset.v1）精选
**10 帧**入资产体系（分层代表 + 暗色×3 + 序列×4 + 18 条 grounding 任务；
字节仍以内容寻址住在 validation/，manifest 引用不复制）：

| frameId 前缀 | stratum | 特征 |
|---|---|---|
| 058f62426c1f | settings | 契约测试同帧（FSV001Arms fixtures） |
| 29f34bdb9dac | settings | 页面/对话框过渡序列 |
| 3682353fffbf | list | scroll 序列 |
| c1667d8b209e | list | click before/after 序列对 |
| 4909995071e6 | dialog | 最富 GT（20 元素） |
| 518f41240dd0 | text-heavy | B1 最强分层 + 暗色 |
| 9ee712f0f817 | icon-heavy | B1 最弱分层 + 暗色 |
| a86c6501da91 | scrollable | 滚动中层代表 |
| 1e572c8f5092 | dense | 双序列引用 |
| 206556c78e8e | sidebar | 暗色 |

roles = CALIBRATION+REGRESSION；**GT 方法 = a11y 辅助 + 确定性校正
（FSV-001 D6r）**——分数只作相对比较（同 GT 跨臂），绝对值含已知偏差
（见 manifest source_relations.gtMethod 与 gt/CALIBRATION-REPORT.md）。
其余 26 帧 calibrated + 3 rejected 留在 validation/ 目录随取（未登记）。
