# Vision Deployment Identity Promotion — Decision Record

> Status: PROMOTED（PROJECT_LEADER_VISION_DEPLOYMENT_PROMOTION_AUTHORITY = APPROVED）
> Date: 2026-08-17
> Prerequisites: VISION_DEPLOYMENT_IDENTITY_ADMISSION_GATE = A（candidate ready）
> Scope: ONE canonical governance admission transaction（B1b 解析）— 零感知/零
> bootstrap/零身份派生/零验证语义变更。

## Previous admitted identity

```
schemaVersion:     uniclaw.localVisionEvidence.v1
modelId:           3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782
configId:          config:edb7ad546d2b7f9c5b2b41affca70c13953e9efbbb5e2347c7418583778ac48f
pipelineRevision:  prev:9e31f8d6d49e7e90f3ac1357bab11e4a7c083b005c4c501bc21a1b3146499bea
deploymentId:      deploy:64f4b88ddaf5a964d80a9877fe93152eb239c0aa7ad9625273d52cd77c342f40
```

## Candidate identity（recomputed twice, identical — repeatable）

```
schemaVersion:     uniclaw.localVisionEvidence.v1
modelId:           3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782
configId:          config:edb7ad546d2b7f9c5b2b41affca70c13953e9efbbb5e2347c7418583778ac48f
pipelineRevision:  prev:c5f506884a60c0b2e4d7ba929005e56956996774be4c421a12b4c2c6eb8bf83c
deploymentId:      deploy:60c84225ff2e362bf37035371f16bb0e252149b3434f727b8727440643850d72
```

## Changed axes

- `pipelineRevision`: CHANGED（9e31f8d6… → c5f50688…）
- `deploymentId`: CHANGED（组合派生，随 pipelineRevision — 非独立轴）
- `modelId` / `configId`: SAME（模型与配置工件未变）

## Source cause

Intentional committed perception pipeline source changes after the previous
admission（`41e322f feat(perception): evaluation, training, tests updates`,
`b7a2a11 Perception Platform Phase 4 …`）— pipelineRevision 为内容哈希派生。

## Authorization

`PROJECT_LEADER_VISION_DEPLOYMENT_PROMOTION_AUTHORITY = APPROVED`
（gate 输入明确授权；按仓库治理惯例 human/Project Leader promotion authority —
Phase 4 governance；不发明新签名/审批机制）。

## Evidence

- deterministic derivation（两次运行完全一致；内容哈希，零 cwd/时间戳依赖）
- candidate validation（modelId=best.pt 内容哈希 / configId=config 内容哈希 /
  pipelineRevision=已提交源码哈希 / deploymentId=canonical 组合 — 全部与 live 服务
  报告一致）
- repository commits/source evidence（工作树干净；漂移根因 = 已提交有意变更）
- receipt 在 admission 前保持只读（git-clean）

## Transaction

1. pre-admission recompute（两次一致 == 批准候选）→ PASS
2. promotion decision recorded（本记录）
3. atomic-writer repair（build_active_identity.py：temp + os.replace）
4. complete receipt regenerated via canonical builder
5. pre-activation validation（四轴 + deploymentId 组合 + factory 接受）
6. atomic activation（os.replace）→ CURRENT_ACTIVE == VALIDATED_CANDIDATE
7. identity guards re-run（CORR_HOST03/04）→ truthful acceptance（verifier 未改）
8. managed Vision smoke
9. regression classification
