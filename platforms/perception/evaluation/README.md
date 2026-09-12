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
