# CORPUS-002 — 评测资产与错题集迁移（体系照搬）
lifecycle_state: closed · disposition: implemented · depth: standard · base: 9a7e1c97

## Intent（WHAT/WHY）
uni-agent 的评测资产体系（正交九维分类 + 内容寻址 + groundtruth 分离 +
suite 版本化）从未迁移——错题集（REGRESSION/CHALLENGE/CALIBRATION 角色
资产）是 OPT-001 后端对照的回归保护地基。Human 裁决：资产迁移先行；
体系合理直接照搬。

## Decisions
- D1 **体系评估 = 合理，照搬**：内容寻址身份（与 RawArtifact/四层身份
  同族）、正交分类（非 mega-enum）、诚实 admission 分级、write-once——
  与仓库 DNA 一致，零重构。
- D2 落地 `platforms/perception/evaluation/`（provider 域，bench 同侧；
  不入 uniclaw_perception 运行包 → pipelineRevision 不含评测代码）。
- D3 golden case-a 字节双驻（corpus 做 parity 锚 / evaluation 做 GOLDEN
  角色）——内容寻址天然去重语义，assetId 一致。

## Scope（已执行）
- 体系资产迁移 25 文件（~800KB）：fixtures×2 + groundtruth×3 + 既有
  manifest×7（含 2 份此前缺字节原始登记）+ golden-run-v1 完整三帧
  （**含 case-b-on/off 开关翻转对**）+ perception JSON×3 + trace×2 +
  wifi-slice2 校准集 + settings-home + 3 份 INFORMATIONAL 证据 JSON +
  scenario-catalog。
- 新登记 manifest×6（golden×3 / settings-home / wifi×2，正交分类照搬
  体系维度）。
- **彩蛋**：settings-home-api35-full.png 哈希与既有 manifest b057d131…
  吻合——"缺字节" CALIBRATION 资产找回，其 groundtruth（gt-b057d131）
  重新有主。
- README 索引（资产清单 + roles + admission + 使用入口 + 已知限制）。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >-
    内容寻址一致性校验（每 manifest content_hash == sha256(可寻字节)）+
    迁移资产 bench 冒烟（case-b-off 全管线推理）+ 全量回归。
  expected: 9/10 manifest 字节绑定 OK（仅 settings-diag 如实缺字节）；
    bench 正常出报告；全量零破坏。
  actual: >-
    校验 9 OK / 1 MISSING（0ef2b572 settings-diag 字节不在 uni-agent git，
    manifest 保留 NEEDS_GROUND_TRUTH 标注）；case-b-off bench 单输出哈希
    GREEN；全量 Kernel 353/353 + Agent 17/17。
  evidence: 本 state + evaluation/README.md
```

## Status log
2026-09-12 · enter→understanding · 考古 asset.py/suite.py/groundtruth/
  manifest 结构；Human 指令：合理照搬，不合理停下讨论。
2026-09-12 · understanding→resolved→implemented · 体系评估 D1（合理）；
  字节盘点（7 manifest 中 3 有字节 4 缺→其中 settings-home 经 tests 侧
  找回）；25 文件迁移 + 6 新登记 + README。
2026-09-12 · implemented→closed · 一致性校验 9/1 + bench 冒烟 + 全量
  353/17 GREEN；OPT-001 的回归保护面就绪。
